using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.Annotations;
using Velune.Windows.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Velune.Windows.Controls;

public sealed partial class AnnotationPanelControl : UserControl
{
    private bool _isCapturingSignaturePad;

    public AnnotationPanelControl()
    {
        InitializeComponent();
    }

    public event EventHandler? AnnotationTextDraftSubmitted;

    public WindowsMainViewModel? ViewModel
    {
        get => DataContext as WindowsMainViewModel;
        set => DataContext = value;
    }

    private void OnAnnotationListItemTapped(object sender, TappedRoutedEventArgs e)
    {
        SelectAnnotationListItem(sender);
    }

    private void OnAnnotationListItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not VirtualKey.Enter and not VirtualKey.Space)
        {
            return;
        }

        SelectAnnotationListItem(sender);
        e.Handled = true;
    }

    private void SelectAnnotationListItem(object sender)
    {
        if (ViewModel is { } viewModel &&
            sender is FrameworkElement { DataContext: WindowsAnnotationOverlayViewModel overlay })
        {
            viewModel.SelectedAnnotationId = overlay.Id;
        }
    }

    private void OnAnnotationToolClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: WindowsAnnotationToolItem tool } &&
            ViewModel.SelectAnnotationToolCommand.CanExecute(tool.Tool))
        {
            ViewModel.SelectAnnotationToolCommand.Execute(tool.Tool);
        }
    }

    private void OnAnnotationColorClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color } &&
            ViewModel.SelectAnnotationColorCommand.CanExecute(color))
        {
            ViewModel.SelectAnnotationColorCommand.Execute(color);
        }
    }

    private void OnAnnotationFillColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color })
        {
            ViewModel?.SelectAnnotationFillColor(color);
        }
    }

    private void OnAnnotationTextTransparentBackgroundClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.AnnotationFillEnabled = false;
        }
    }

    private void OnAnnotationBorderColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color })
        {
            ViewModel?.SelectAnnotationBorderColor(color);
        }
    }

    private void OnSignatureAssetClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: SignatureAsset asset } &&
            ViewModel.SelectSignatureAssetCommand.CanExecute(asset.Id))
        {
            ViewModel.SelectSignatureAssetCommand.Execute(asset.Id);
        }
    }

    private void OnDeleteSignatureAssetClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: SignatureAsset asset } &&
            ViewModel.DeleteSelectedSignatureAssetCommand.CanExecute(asset.Id))
        {
            ViewModel.DeleteSelectedSignatureAssetCommand.Execute(asset.Id);
        }
    }

    private void OnAnnotationTextDraftKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not VirtualKey.Enter)
        {
            return;
        }

        CoreVirtualKeyStates shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if (!shiftState.HasFlag(CoreVirtualKeyStates.Down))
        {
            AnnotationTextDraftSubmitted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnDeleteAnnotationTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        Guid annotationId = sender is FrameworkElement { DataContext: WindowsAnnotationOverlayViewModel overlay }
            ? overlay.Id
            : sender is FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment }
                ? comment.Id
                : Guid.Empty;

        if (annotationId == Guid.Empty ||
            !ViewModel.DeleteAnnotationByIdCommand.CanExecute(annotationId))
        {
            return;
        }

        ViewModel.DeleteAnnotationByIdCommand.Execute(annotationId);
        e.Handled = true;
    }

    private void OnAnnotationMenuEditClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.BeginEditAnnotationById(id);
        }
    }

    private void OnAnnotationMenuHideClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.ToggleAnnotationVisibility(id);
        }
    }

    private void OnAnnotationMenuLockClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.ToggleAnnotationLock(id);
        }
    }

    private void OnAnnotationMenuDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            ResolveAnnotationIdFromMenuContext(sender) is { } id &&
            ViewModel.DeleteAnnotationByIdCommand.CanExecute(id))
        {
            ViewModel.DeleteAnnotationByIdCommand.Execute(id);
        }
    }

    private static Guid? ResolveAnnotationIdFromMenuContext(object sender)
    {
        if (sender is FrameworkElement { Tag: Guid tagId })
        {
            return tagId;
        }

        if (sender is not FrameworkElement element)
        {
            return null;
        }

        FrameworkElement? parent = element;
        while (parent is not null)
        {
            switch (parent.DataContext)
            {
                case WindowsAnnotationOverlayViewModel overlay:
                    return overlay.Id;
                case WindowsCommentOverlayViewModel comment:
                    return comment.Id;
                default:
                    parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
                    break;
            }
        }

        return null;
    }

    private void OnSignaturePadPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null ||
            sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        _isCapturingSignaturePad = true;
        ViewModel.BeginSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSignaturePadPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null ||
            !_isCapturingSignaturePad ||
            sender is not FrameworkElement layer)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        ViewModel.UpdateSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        e.Handled = true;
    }

    private void OnSignaturePadPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CompleteSignatureCapture(sender, e);
    }

    private void OnSignaturePadPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CompleteSignatureCapture(sender, e);
    }

    private void CompleteSignatureCapture(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null ||
            !_isCapturingSignaturePad)
        {
            return;
        }

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        ViewModel.CompleteSignatureCapture();
        _isCapturingSignaturePad = false;
        e.Handled = true;
    }
}
