namespace VideoEffects;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System.Collections.Generic;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;

public static class PropertySetExtensionsForColorAdjustmentEffect
{
    public static bool IsDefaultBrightness(float v) => v == 0;
    public static bool IsDefaultContrast(float v) => v == 1;
    public static bool IsDefaultSaturation(float v) => v == 1;

    public static void SetBrightness(this IPropertySet ps, float value)
    {
        ps["Brightness"] = value;        
    }

    public static void SetContrast(this IPropertySet ps, float value)
    {
        ps["Contrast"] = value;
    }

    public static void SetSaturation(this IPropertySet ps, float value)
    {
        ps["Saturation"] = value;
    }
}

public sealed class ColorAdjustmentEffect : IBasicVideoEffect
{
    private CanvasDevice _canvasDevice;
    private ColorMatrixEffect _colorMatrixEffect;
    private SaturationEffect _saturationEffect;
    private PropertySet _properties;

    // パラメーター（デフォルト値）
    private float _brightness = 0.0f; // Exposure: -1.0 〜 1.0
    private float _contrast = 1.0f;   // Contrast: 0.0 〜 2.0
    private float _saturation = 1.0f; // Saturation: 0.0（モノクロ）〜 2.0+

    // GPU描画処理の有効化
    public bool UseGraphicsDeviceGpu => true;
    public MediaMemoryTypes SupportedMemoryTypes => MediaMemoryTypes.Gpu;
    public bool IsReadOnly => false;
    public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties => new List<VideoEncodingProperties>();

    public bool TimeIndependent => true;

    public void SetProperties(IPropertySet configuration)
    {
        _properties = (PropertySet)configuration;
    }

    public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
    {
        // Media Foundation から渡された Direct3D デバイスから CanvasDevice を作成
        _canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
        _colorMatrixEffect = new ColorMatrixEffect();
        _saturationEffect = new SaturationEffect();
        _saturationEffect.Source = _colorMatrixEffect;
    }

    public void ProcessFrame(ProcessVideoFrameContext context)
    {
        // 外部から変更されたプロパティを読み込む
        UpdateParameters();
        
        // 入力フレームと出力フレームの D3D サーフェスから CanvasBitmap / CanvasRenderTarget を作成
        using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, context.InputFrame.Direct3DSurface))
        using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(_canvasDevice, context.OutputFrame.Direct3DSurface))
        using (var ds = renderTarget.CreateDrawingSession())
        {
            float offset = (1.0f - _contrast) * 0.5f + _brightness;

            _colorMatrixEffect.Source = inputBitmap;
            _colorMatrixEffect.ColorMatrix = new Matrix5x4
            {
                M11 = _contrast, M12 = 0,        M13 = 0,        M14 = 0,
                M21 = 0,        M22 = _contrast, M23 = 0,        M24 = 0,
                M31 = 0,        M32 = 0,        M33 = _contrast, M34 = 0,
                M41 = 0,        M42 = 0,        M43 = 0,        M44 = 1,
                M51 = offset,   M52 = offset,   M53 = offset,   M54 = 0
            };
            // 3. 彩度 (SaturationEffect)
            _saturationEffect.Saturation = _saturation;

            // 最終結果を出力サーフェスに描画
            ds.DrawImage(_saturationEffect);
        }
    }

    private void UpdateParameters()
    {
        if (_properties == null) return;

        if (_properties.TryGetValue("Brightness", out object b) && b is float fb)
            _brightness = fb;
        if (_properties.TryGetValue("Contrast", out object c) && c is float fc)
            _contrast = fc;
        if (_properties.TryGetValue("Saturation", out object s) && s is float fs)
            _saturation = fs;
    }

    public void Close(MediaEffectClosedReason reason)
    {
        _canvasDevice?.Dispose();
        _colorMatrixEffect?.Dispose();
    }

    public void DiscardQueuedFrames() { }
}
