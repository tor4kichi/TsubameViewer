﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

// コンテンツ ダイアログの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views.Dialogs
{
    public sealed partial class SelectItemDialog : ContentDialog
    {
        public SelectItemDialog(string title, string confirmButtonText)
        {
            this.InitializeComponent();
            Title = title;
            PrimaryButtonText = confirmButtonText;
            CloseButtonClick += SelectItemDialog_CloseButtonClick;
            PrimaryButtonClick += SelectItemDialog_PrimaryButtonClick;

            Opened += SelectItemDialog_Opened;
        }

        private async void SelectItemDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            await Task.Delay(5);

            if (_selectItems != null)
            {
                int index = 0;
                foreach (var item in ItemsSource)
                {
                    if (_selectItems.Contains(item))
                    {
                        MyListView.SelectRange(new ItemIndexRange(index, 1));
                    }

                    index++;
                }

                _selectItems = null;
            }
        }

        private void SelectItemDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsOptionRequrested = false;
        }

        private void SelectItemDialog_OptionButtonClick(object sender, RoutedEventArgs args)
        {
            IsOptionRequrested = true;
            MyListView.SelectedItems.Clear();
            this.Hide();
        }

               
        private void SelectItemDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsOptionRequrested = false;
            MyListView.SelectedItems.Clear();
        }

        public bool IsOptionRequrested { get; private set; }

        public IList ItemsSource
        {
            get { return (IList)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IList), typeof(SelectItemDialog), new PropertyMetadata(null));




        public string DisplayMemberPath
        {
            get { return (string)GetValue(DisplayMemberPathProperty); }
            set { SetValue(DisplayMemberPathProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DisplayMemberPath.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register("DisplayMemberPath", typeof(string), typeof(SelectItemDialog), new PropertyMetadata(null));



        IEnumerable<object> _selectItems;

        public void SetSelectedItems(IEnumerable<object> selection)
        {
            _selectItems = selection;
        }

        public IList<object> GetSelectedItems()
        {
            return MyListView.SelectedItems.ToList();
        }
    }
}
