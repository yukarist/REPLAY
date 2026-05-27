using Microsoft.Xaml.Behaviors;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace REPLAY.UI.Behaviors
{
    public class AutoScrollBehavior : Behavior<ListBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject.Items is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (AssociatedObject.DataContext is MainViewModel vm && !vm.IsAutoScrollEnabled)
                return;

            if (e.Action != NotifyCollectionChangedAction.Add)
                return;

            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                var scrollViewer = GetScrollViewer(AssociatedObject);
                scrollViewer?.ScrollToEnd(); // ←これが本命🔥
            });
        }

        private ScrollViewer? GetScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}