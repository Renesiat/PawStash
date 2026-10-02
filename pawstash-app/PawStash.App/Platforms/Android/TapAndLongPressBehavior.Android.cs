namespace PawStash.Behaviors;

public partial class TapAndLongPressBehavior : PlatformBehavior<View, Android.Views.View>
{
	View? _view;

	protected override void OnAttachedTo(View bindable, Android.Views.View platformView)
	{
		base.OnAttachedTo(bindable, platformView);
		_view = bindable;
		platformView.Clickable = true;
		platformView.LongClickable = true;
		platformView.Click += OnClick;
		platformView.LongClick += OnLongClick;
	}

	protected override void OnDetachedFrom(View bindable, Android.Views.View platformView)
	{
		platformView.Click -= OnClick;
		platformView.LongClick -= OnLongClick;
		_view = null;
		base.OnDetachedFrom(bindable, platformView);
	}

	void OnClick(object? sender, EventArgs e)
	{
		if (_view is not null)
		{
			RaiseTapped(_view);
		}
	}

	void OnLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
	{
		e.Handled = true;

		if (_view is not null)
		{
			RaiseLongPressed(_view);
		}
	}
}
