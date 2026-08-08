using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Specialized;

namespace SSMM_UI.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "IsEnabled",
            typeof(AutoScrollBehavior),
            defaultValue: false);

    private static readonly AttachedProperty<
        INotifyCollectionChanged?> CollectionProperty =
        AvaloniaProperty.RegisterAttached<ListBox, INotifyCollectionChanged?>(
            "Collection",
            typeof(AutoScrollBehavior));

    private static readonly AttachedProperty<
        NotifyCollectionChangedEventHandler?> HandlerProperty =
        AvaloniaProperty.RegisterAttached<ListBox, NotifyCollectionChangedEventHandler?>(
            "Handler",
            typeof(AutoScrollBehavior));

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ListBox>(
            OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ListBox element)
        => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ListBox element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(
        ListBox listBox,
        AvaloniaPropertyChangedEventArgs change)
    {
        if (change.NewValue is true)
        {
            Attach(listBox);
        }
        else
        {
            Detach(listBox);
        }
    }

    private static void Attach(ListBox listBox)
    {
        SubscribeToCollection(listBox);

        listBox.PropertyChanged += OnListBoxPropertyChanged;
    }

    private static void Detach(ListBox listBox)
    {
        listBox.PropertyChanged -= OnListBoxPropertyChanged;

        UnsubscribeFromCollection(listBox);
    }

    private static void OnListBoxPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        if (e.Property == ItemsControl.ItemsSourceProperty)
        {
            SubscribeToCollection(listBox);
        }
    }

    private static void SubscribeToCollection(ListBox listBox)
    {
        UnsubscribeFromCollection(listBox);

        if (listBox.ItemsSource is not INotifyCollectionChanged collection)
            return;

        NotifyCollectionChangedEventHandler handler =
            (_, args) => OnCollectionChanged(listBox, args);

        collection.CollectionChanged += handler;

        listBox.SetValue(CollectionProperty, collection);
        listBox.SetValue(HandlerProperty, handler);
    }

    private static void UnsubscribeFromCollection(ListBox listBox)
    {
        var collection = listBox.GetValue(CollectionProperty);
        var handler = listBox.GetValue(HandlerProperty);

        if (collection is not null && handler is not null)
        {
            collection.CollectionChanged -= handler;
        }

        listBox.SetValue(CollectionProperty, null);
        listBox.SetValue(HandlerProperty, null);
    }

    private static void OnCollectionChanged(
        ListBox listBox,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        if (listBox.ItemCount == 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (listBox.ItemCount == 0)
                return;

            listBox.ScrollIntoView(listBox.ItemCount - 1);
        });
    }
}