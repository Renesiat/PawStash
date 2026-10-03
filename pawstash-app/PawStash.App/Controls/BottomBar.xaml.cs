using PawStash.Common.Enums;
using PawStash.Common.Rules;
using PawStash.Services;

namespace PawStash.Controls;

public partial class BottomBar : ContentView
{
	public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
		nameof(ActiveTab),
		typeof(BottomBarTab),
		typeof(BottomBar),
		BottomBarTab.Home,
		propertyChanged: (bindable, _, _) => ((BottomBar)bindable).UpdateTabs());

	public static readonly BindableProperty CurrentFolderIdProperty = BindableProperty.Create(
		nameof(CurrentFolderId),
		typeof(Guid?),
		typeof(BottomBar));

	public BottomBar()
	{
		InitializeComponent();
		BarCanvas.Drawable = new BottomBarDrawable();
		UpdateTabs();
	}

	public BottomBarTab ActiveTab
	{
		get => (BottomBarTab)GetValue(ActiveTabProperty);
		set => SetValue(ActiveTabProperty, value);
	}

	public Guid? CurrentFolderId
	{
		get => (Guid?)GetValue(CurrentFolderIdProperty);
		set => SetValue(CurrentFolderIdProperty, value);
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();

		if (Application.Current is null)
		{
			return;
		}

		if (Handler is null)
		{
			Application.Current.RequestedThemeChanged -= OnThemeChanged;
		}
		else
		{
			Application.Current.RequestedThemeChanged += OnThemeChanged;
		}
	}

	void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => BarCanvas.Invalidate();

	void UpdateTabs()
	{
		bool isHome = ActiveTab == BottomBarTab.Home;

		SetTabIcon(HomeIcon, "icon_home", isHome);
		SetTabIcon(ProfileIcon, "icon_profile", !isHome);
		HomeDot.IsVisible = isHome;
		ProfileDot.IsVisible = !isHome;
	}

	static void SetTabIcon(Image icon, string name, bool isActive)
	{
		if (isActive)
		{
			icon.SetAppTheme<ImageSource>(Image.SourceProperty, $"{name}_active_light.png", $"{name}_active_dark.png");
		}
		else
		{
			icon.Source = $"{name}_inactive.png";
		}
	}

	async void OnHomeTapped(object? sender, TappedEventArgs e)
	{
		CloseMenu();
		await Shell.Current.GoToAsync("//home");
	}

	async void OnProfileTapped(object? sender, TappedEventArgs e)
	{
		CloseMenu();
		await Shell.Current.GoToAsync("//profile");
	}

	async void OnPlusTapped(object? sender, TappedEventArgs e)
	{
		if (CreateMenu.IsVisible)
		{
			CloseMenu();
			return;
		}

		Dimmer.IsVisible = true;
		CreateMenu.IsVisible = true;

		await Task.WhenAll(
			Dimmer.FadeToAsync(0.25, 150),
			CreateMenu.FadeToAsync(1, 150),
			PlusIcon.RotateToAsync(45, 150));
	}

	void OnDimmerTapped(object? sender, TappedEventArgs e) => CloseMenu();

	void CloseMenu()
	{
		Dimmer.Opacity = 0;
		Dimmer.IsVisible = false;
		CreateMenu.Opacity = 0;
		CreateMenu.IsVisible = false;
		PlusIcon.Rotation = 0;
	}

	async void OnCreateLinkTapped(object? sender, TappedEventArgs e) => await OpenCreateFormAsync(FileSystemItemType.Link);

	async void OnCreateNoteTapped(object? sender, TappedEventArgs e) => await OpenCreateFormAsync(FileSystemItemType.Note);

	async void OnCreateFolderTapped(object? sender, TappedEventArgs e) => await OpenCreateFormAsync(FileSystemItemType.Folder);

	async Task OpenCreateFormAsync(FileSystemItemType itemType, PickedFile? file = null)
	{
		CloseMenu();

		ShellNavigationQueryParameters query = new() { ["itemType"] = itemType };

		if (CurrentFolderId is Guid folderId)
		{
			query["parentFolderId"] = folderId;
		}

		if (file is not null)
		{
			query["file"] = file;
		}

		await Shell.Current.GoToAsync("item", query);
	}

	async void OnUploadFileTapped(object? sender, TappedEventArgs e)
	{
		CloseMenu();

		ItemCreationService itemCreation = IPlatformApplication.Current!.Services.GetRequiredService<ItemCreationService>();
		PickedFile? file = await itemCreation.PickFileAsync();

		if (file is null)
		{
			return;
		}

		string? error = ItemCreationService.Validate(file);

		if (error is not null)
		{
			await Shell.Current.DisplayAlertAsync("Файл не додано", error, "OK");
			return;
		}

		await OpenCreateFormAsync(FileSystemItemRules.GetUploadedFileType(file.FileName)!.Value, file);
	}
}
