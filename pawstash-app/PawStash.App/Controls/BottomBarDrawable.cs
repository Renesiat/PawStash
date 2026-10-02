namespace PawStash.Controls;

public class BottomBarDrawable : IDrawable
{
	public const float Inset = 12f;
	public const float BodyTop = 34f;
	public const float PlusCenterY = 66f;

	const float BumpHeight = 16f;
	const float BumpHalfWidth = 78f;
	const float CornerRadius = 32f;
	const float RingRadius = 44f;
	const int RingDots = 40;

	static readonly Color Accent = Color.FromArgb("#7C5CFF");

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		float left = Inset;
		float right = dirtyRect.Width - Inset;
		float bottom = dirtyRect.Height - Inset;
		float centerX = dirtyRect.Width / 2f;

		PathF bar = new();
		bar.MoveTo(left, BodyTop + CornerRadius);
		bar.QuadTo(left, BodyTop, left + CornerRadius, BodyTop);
		bar.LineTo(centerX - BumpHalfWidth, BodyTop);
		bar.CurveTo(centerX - BumpHalfWidth * 0.45f, BodyTop, centerX - BumpHalfWidth * 0.55f, BodyTop - BumpHeight, centerX, BodyTop - BumpHeight);
		bar.CurveTo(centerX + BumpHalfWidth * 0.55f, BodyTop - BumpHeight, centerX + BumpHalfWidth * 0.45f, BodyTop, centerX + BumpHalfWidth, BodyTop);
		bar.LineTo(right - CornerRadius, BodyTop);
		bar.QuadTo(right, BodyTop, right, BodyTop + CornerRadius);
		bar.LineTo(right, bottom - CornerRadius);
		bar.QuadTo(right, bottom, right - CornerRadius, bottom);
		bar.LineTo(left + CornerRadius, bottom);
		bar.QuadTo(left, bottom, left, bottom - CornerRadius);
		bar.Close();

		canvas.SaveState();
		canvas.SetShadow(new SizeF(0, 6), 22, isDark ? Color.FromRgba(0, 0, 0, 140) : Color.FromRgba(20, 20, 40, 34));
		canvas.FillColor = isDark ? Color.FromArgb("#232326") : Colors.White;
		canvas.FillPath(bar);
		canvas.RestoreState();

		if (!isDark)
		{
			canvas.FillColor = Accent.WithAlpha(0.45f);

			for (int dot = 0; dot < RingDots; dot++)
			{
				double angle = 2 * Math.PI * dot / RingDots;
				float x = centerX + RingRadius * (float)Math.Cos(angle);
				float y = PlusCenterY + RingRadius * (float)Math.Sin(angle);

				canvas.FillCircle(x, y, 1.2f);
			}
		}
	}
}
