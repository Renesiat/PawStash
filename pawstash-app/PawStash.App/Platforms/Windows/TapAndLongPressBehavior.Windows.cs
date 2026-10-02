using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace PawStash.Behaviors;

public partial class TapAndLongPressBehavior : PlatformBehavior<View, FrameworkElement>
{
	View? _view;

	protected override void OnAttachedTo(View bindable, FrameworkElement platformView)
	{
		base.OnAttachedTo(bindable, platformView);
		_view = bindable;
		platformView.Tapped += OnTapped;
		platformView.RightTapped += OnRightTapped;
	}

	protected override void OnDetachedFrom(View bindable, FrameworkElement platformView)
	{
		platformView.Tapped -= OnTapped;
		platformView.RightTapped -= OnRightTapped;
		_view = null;
		base.OnDetachedFrom(bindable, platformView);
	}

	void OnTapped(object sender, TappedRoutedEventArgs e)
	{
		if (_view is not null)
		{
			RaiseTapped(_view);
		}
	}

	void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		if (_view is not null)
		{
			RaiseLongPressed(_view);
		}
	}
}
