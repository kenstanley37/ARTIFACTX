using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NMS.WinUI3.ViewModels;
using System;
using Windows.UI;

namespace NMS.WinUI3.Converters;

public class SlotStateToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is InventorySlotState state
            ? state switch
            {
                InventorySlotState.Occupied => new SolidColorBrush(Color.FromArgb(60, 255, 157, 0)),
                InventorySlotState.UnlockedEmpty => new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                _ => new SolidColorBrush(Colors.Transparent)
            }
            : new SolidColorBrush(Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class SlotStateToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is InventorySlotState state && state != InventorySlotState.Locked
            ? new SolidColorBrush(Color.FromArgb(255, 90, 98, 112))
            : new SolidColorBrush(Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}