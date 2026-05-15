using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace TsubameViewer.Services;

public sealed class PurchaseAddonService
{
    const string CheerAddonStoreId = "9P0WVC37NP3G";


    public async Task<StorePurchaseStatus> PurchaseCheerAsync()
    {
        return await PurchaseAndConsumeAddOn(CheerAddonStoreId);
    }

    public async Task<StoreProduct?> GetCheerAddonInfoAsync()
    {
        StoreContext context = StoreContext.GetDefault();
        var cons = await context.GetAssociatedStoreProductsAsync(["Consumable"]);
        return cons.Products.TryGetValue(CheerAddonStoreId, out var sp) ? sp : null;
    }
    
    private async Task<StorePurchaseStatus> PurchaseAndConsumeAddOn(string storeId)
    {
        StoreContext context = StoreContext.GetDefault();

        // 1. 購入処理の開始
        StorePurchaseResult result = await context.RequestPurchaseAsync(storeId);

        // 2. 購入結果の確認
        switch (result.Status)
        {
            case StorePurchaseStatus.Succeeded:
                // 購入成功：すぐに「消費」プロセスへ
                await FulfillAddOn(storeId);
                break;

            case StorePurchaseStatus.AlreadyPurchased:
                // すでに購入済みだが未消費の場合も、ここで消費処理を走らせる
                await FulfillAddOn(storeId);
                break;

            case StorePurchaseStatus.NotPurchased:
                // ユーザーが購入をキャンセルした場合
                break;

            case StorePurchaseStatus.NetworkError:
            case StorePurchaseStatus.ServerError:
                // エラー処理
                break;
            default:
                break;
        }

        return result.Status;
    }

    private async Task FulfillAddOn(string storeId)
    {
        StoreContext context = StoreContext.GetDefault();

        // トランザクションIDが必要なため、一度未消費の購入情報を取得
        StoreConsumableResult consumables = await context.GetConsumableBalanceRemainingAsync(storeId);

        // 3. 消費（フルフィルメント）の報告
        // trackingId（GUID）を生成して送信することで、多重消費を防止します
        Guid trackingId = Guid.NewGuid();
        StoreConsumableResult fulfillmentResult = await context.ReportConsumableFulfillmentAsync(storeId, 1, trackingId);

        if (fulfillmentResult.Status == StoreConsumableStatus.Succeeded)
        {
            // ここでユーザーにアイテム（コインや機能など）を付与する処理を行う
            System.Diagnostics.Debug.WriteLine("アドオンの消費が完了しました。");
        }
    }
}
