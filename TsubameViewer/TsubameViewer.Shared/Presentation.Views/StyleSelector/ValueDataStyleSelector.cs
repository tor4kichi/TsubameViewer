using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace TsubameViewer.Presentation.Views.StyleSelector
{
    public partial class ValueBasedStyleSelectorExtension : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty =
           DependencyProperty.RegisterAttached(
               "Value",
               typeof(object),
               typeof(ValueBasedStyleSelectorExtension),
               new PropertyMetadata(default(Style))
           );

        public static void SetValue(DependencyObject element, object value)
        {
            element.SetValue(ValueProperty, value);
        }
        public static object GetValue(DependencyObject element)
        {
            return element.GetValue(ValueProperty);
        }
    }

    public class StylesCollection : Collection<Style> { }

    [ContentProperty(Name = "Styles")]
    public class ValueBasedStyleSelector : Windows.UI.Xaml.Controls.StyleSelector
    {
        public string FieldName { get; set; }
        public string PropertyName { get; set; }

        public Style Default { get; set; }

        public bool ForceCompereWithString { get; set; }

        public StylesCollection Styles { get; set; } = new StylesCollection();

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item == null)
            {
                return Default ?? base.SelectStyleCore(item, container);

            }

            object value = null;
            if (string.IsNullOrEmpty(FieldName) && string.IsNullOrEmpty(PropertyName))
            {
                value = item;
            }
            else
            {
                var itemType = item.GetType();

                // check field member value
                if (!string.IsNullOrEmpty(FieldName))
                {
                    var fieldInfo = itemType.GetField(FieldName);
                    if (fieldInfo?.IsPublic ?? false)
                    {
                        value = fieldInfo.GetValue(item);
                    }
                }

                // check property member value
                if (value == null && !string.IsNullOrEmpty(PropertyName))
                {
                    var propInfo = itemType.GetProperty(PropertyName);
                    if (propInfo?.CanRead ?? false)
                    {
                        value = propInfo.GetValue(item);
                    }
                }
            }


            // compare values, and choose template
            if (value != null)
            {
                bool valueIsString = value is string;
                var strSourceValue = value.ToString();
                foreach (var style in Styles)
                {
                    var styleValue = ValueBasedStyleSelectorExtension.GetValue(style);
                    if (ForceCompereWithString || valueIsString)
                    {
                        if (styleValue.ToString() == strSourceValue)
                        {
                            return style;
                        }
                    }
                    else if (styleValue is string strDestValue)
                    {
                        if (strSourceValue == strDestValue)
                        {
                            return style;
                        }
                    }
                    else if (styleValue.Equals(value))
                    {
                        return style;
                    }
                }
            }

            return Default ?? base.SelectStyleCore(item, container);
        }
    }
}
