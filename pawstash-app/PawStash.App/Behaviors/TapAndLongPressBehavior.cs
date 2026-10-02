namespace PawStash.Behaviors;

public class PressedEventArgs(object? item) : EventArgs
{
	public object? Item { get; } = item;
}

public partial class TapAndLongPressBehavior
{
	public event EventHandler<PressedEventArgs>? Tapped;

	public event EventHandler<PressedEventArgs>? LongPressed;

	void RaiseTapped(View view) => Tapped?.Invoke(view, new PressedEventArgs(view.BindingContext));

	void RaiseLongPressed(View view) => LongPressed?.Invoke(view, new PressedEventArgs(view.BindingContext));
}
