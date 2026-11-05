using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.Templates;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

namespace HabitGrid.WinUI.Pages;

/// <summary>Luồng làm quen toàn trang. Trạng thái ở <see cref="OnboardingViewModel"/>; trang chỉ dựng giao diện và chuyển bước.</summary>
public sealed partial class OnboardingPage : Page
{
    private bool _syncing;

    public OnboardingPage()
    {
        ViewModel = App.Host.Get<OnboardingViewModel>();
        InitializeComponent();
        BuildStaticTexts();
    }

    public OnboardingViewModel ViewModel { get; }

    /// <summary>Phát khi hoàn tất hoặc bỏ qua; <c>null</c> nghĩa là bỏ qua.</summary>
    public event EventHandler<OnboardingResult?>? Completed;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            await ViewModel.RestoreAsync();
            RebuildAll();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Mở onboarding lỗi", ex);
        }
    }

    // ------------------------------------------------------------------ dựng giao diện

    private void BuildStaticTexts()
    {
        // Tắt hiệu ứng xuất hiện dần của GridView: dựng lại danh sách không được nhấp nháy.
        TopicGrid.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
        SuggestionGrid.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
        BuddyColorGrid.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();

        WelcomeTitle.Text = Ui.T("onboarding.welcome.title");
        WelcomeBody.Text = Ui.T("onboarding.welcome.body");
        StartButton.Content = Ui.T("onboarding.start");
        SkipButton.Content = Ui.T("onboarding.haveData");
        GoalsTitle.Text = Ui.T("onboarding.goals.title");
        GoalsHint.Text = Ui.T("onboarding.goals.hint");
        HabitsTitle.Text = Ui.T("onboarding.habits.title");
        HabitsHint.Text = Ui.T("onboarding.habits.hint");
        CustomBox.PlaceholderText = Ui.T("onboarding.custom.placeholder");
        CustomAddButton.Content = Ui.T("onboarding.custom.add");
        BuddyTitle.Text = Ui.T("onboarding.buddy.title");
        BuddyBody.Text = Ui.T("onboarding.buddy.body");
        ReminderTitle.Text = Ui.T("onboarding.reminder.title");
        ReminderBody.Text = Ui.T("onboarding.reminder.body");
        BackButton.Content = Ui.T("onboarding.back");

        foreach (var topic in OnboardingViewModel.AllTopics)
        {
            var toggle = new ToggleButton
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = Ui.TopicEmoji(topic), FontSize = 20 },
                        new TextBlock { Text = Ui.TopicTitle(topic), VerticalAlignment = VerticalAlignment.Center }
                    }
                },
                Tag = topic,
                Style = (Style)Application.Current.Resources["ChipStyle"],
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(18, 10, 18, 10)
            };
            toggle.Click += OnTopicClick;
            TopicGrid.Items.Add(toggle);
        }

        foreach (var hex in HabitCreationService.PresetColors)
        {
            BuddyColorGrid.Items.Add(new Ellipse { Width = 28, Height = 28, Fill = Ui.Brush(hex), Tag = hex, Margin = new Thickness(4) });
        }

        foreach (var key in new[] { "reminder.none", "reminder.morning", "reminder.evening", "reminder.custom" })
        {
            ReminderRadio.Add(Ui.T(key));
        }

        for (var i = 0; i < OnboardingViewModel.StepCount; i++)
        {
            Dots.Children.Add(new Ellipse { Width = 8, Height = 8 });
        }
    }

    private void RebuildAll()
    {
        _syncing = true;

        try
        {
            foreach (var toggle in TopicGrid.Items.OfType<ToggleButton>())
            {
                toggle.IsChecked = ViewModel.SelectedTopics.Contains((HabitTopic)toggle.Tag);
            }

            BuddyNameBox.Text = ViewModel.BuddyName;
            BuddyColorGrid.SelectedIndex = HabitCreationService.PresetColors.ToList()
                .FindIndex(c => c.Equals(ViewModel.BuddyColor, StringComparison.OrdinalIgnoreCase));
            BuddyBubble.Background = BuddyBrush(ViewModel.BuddyColor);
            ReminderRadio.SelectedIndex = (int)ViewModel.ReminderChoice;
            CustomTimePicker.Time = ViewModel.CustomReminderTime.ToTimeSpan();
            CustomTimePicker.Visibility = ViewModel.ReminderChoice == ReminderChoice.Custom ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _syncing = false;
        }

        RebuildSuggestions();
        RebuildPicked();
        ApplyStep();
    }

    private void RebuildSuggestions()
    {
        SuggestionGrid.Items.Clear();
        var language = Ui.Loc.Language;

        foreach (var template in ViewModel.Suggestions)
        {
            var card = new ToggleButton
            {
                Tag = template,
                Style = (Style)Application.Current.Resources["ChipStyle"],
                IsChecked = ViewModel.IsPicked(template),
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 8, 12, 8),
                Width = 210,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = template.Emoji, FontSize = 20, VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock
                        {
                            Text = template.Name(language),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxWidth = 150,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            card.Click += OnSuggestionClick;
            SuggestionGrid.Items.Add(card);
        }
    }

    private void RebuildPicked()
    {
        PickedList.Items.Clear();

        foreach (var pick in ViewModel.Picked)
        {
            var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock { Text = pick.Emoji, FontSize = 20, VerticalAlignment = VerticalAlignment.Center });

            var name = new TextBox { Text = pick.Name, MaxLength = HabitValidator.MaxNameLength };
            name.TextChanged += (_, _) => pick.Name = name.Text;
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            if (pick.HasNumericTarget)
            {
                var target = new NumberBox
                {
                    Value = pick.Target,
                    Minimum = 1,
                    Maximum = 100000,
                    Width = 96,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
                };
                target.ValueChanged += (_, e) =>
                {
                    if (!double.IsNaN(e.NewValue))
                    {
                        pick.Target = (int)e.NewValue;
                    }
                };
                Grid.SetColumn(target, 2);
                row.Children.Add(target);
            }

            var remove = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 }, Padding = new Thickness(8, 6, 8, 6) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, $"{Ui.T("editor.reminder.remove")}: {pick.Name}");
            remove.Click += (_, _) =>
            {
                ViewModel.RemovePicked(pick);
                RebuildPicked();
                RebuildSuggestions();
                UpdateButtons();
            };
            Grid.SetColumn(remove, 3);
            row.Children.Add(remove);

            PickedList.Items.Add(row);
        }
    }

    private void ApplyStep()
    {
        WelcomePanel.Visibility = Show(OnboardingStep.Welcome);
        GoalsPanel.Visibility = Show(OnboardingStep.Goals);
        HabitsPanel.Visibility = Show(OnboardingStep.Habits);
        BuddyPanel.Visibility = Show(OnboardingStep.Buddy);
        ReminderPanel.Visibility = Show(OnboardingStep.Reminder);
        Footer.Visibility = ViewModel.Step == OnboardingStep.Welcome ? Visibility.Collapsed : Visibility.Visible;

        NextButton.Content = Ui.T(ViewModel.IsLastStep ? "onboarding.finish" : "onboarding.next");
        StepText.Text = Ui.Loc.Format("onboarding.step", ViewModel.StepIndex + 1, OnboardingViewModel.StepCount);

        var accent = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentBrandBrush"];
        var idle = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrokeSubtleBrush"];
        for (var i = 0; i < Dots.Children.Count; i++)
        {
            ((Ellipse)Dots.Children[i]).Fill = i <= ViewModel.StepIndex ? accent : idle;
        }

        UpdateButtons();
    }

    private Visibility Show(OnboardingStep step) => ViewModel.Step == step ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateButtons()
    {
        BackButton.IsEnabled = ViewModel.CanGoBack;
        NextButton.IsEnabled = ViewModel.CanGoNext && !ViewModel.IsBusy;
    }

    // ------------------------------------------------------------------ hỗ trợ kiểm thử

    /// <summary>Bước nào đang hiển thị (cho smoke test kiểm tra giao diện thật sự đổi bước).</summary>
    public bool IsPanelVisible(OnboardingStep step) => step switch
    {
        OnboardingStep.Welcome => WelcomePanel.Visibility == Visibility.Visible,
        OnboardingStep.Goals => GoalsPanel.Visibility == Visibility.Visible,
        OnboardingStep.Habits => HabitsPanel.Visibility == Visibility.Visible,
        OnboardingStep.Buddy => BuddyPanel.Visibility == Visibility.Visible,
        _ => ReminderPanel.Visibility == Visibility.Visible
    };

    /// <summary>Dựng lại giao diện theo ViewModel (smoke test đổi ViewModel trực tiếp rồi gọi hàm này).</summary>
    public void RefreshForSmoke() => RebuildAll();

    // ------------------------------------------------------------------ tương tác

    /// <summary>Sang bước kế (dùng cho nút và smoke test).</summary>
    public async Task GoNextAsync()
    {
        if (ViewModel.IsLastStep)
        {
            await FinishAsync();
            return;
        }

        if (await ViewModel.NextAsync())
        {
            if (ViewModel.Step == OnboardingStep.Habits)
            {
                RebuildSuggestions();
                RebuildPicked();
            }

            ApplyStep();
        }
    }

    public async Task GoBackAsync()
    {
        await ViewModel.BackAsync();
        ApplyStep();
    }

    public async Task FinishAsync()
    {
        FinishError.IsOpen = false;
        var result = await ViewModel.FinishAsync();

        if (result.Succeeded)
        {
            Completed?.Invoke(this, result);
            return;
        }

        FinishError.Message = string.Join(" ", result.Errors.Select(e => ValidationText.For(Ui.Loc, e)).Distinct());
        FinishError.IsOpen = true;
        UpdateButtons();
    }

    private async void OnStartClick(object sender, RoutedEventArgs e) => await Guard(GoNextAsync);

    private async void OnNextClick(object sender, RoutedEventArgs e) => await Guard(GoNextAsync);

    private async void OnBackClick(object sender, RoutedEventArgs e) => await Guard(GoBackAsync);

    private async void OnSkipClick(object sender, RoutedEventArgs e) =>
        await Guard(async () =>
        {
            await ViewModel.SkipAsync();
            Completed?.Invoke(this, null);
        });

    private void OnTopicClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: HabitTopic topic } toggle)
        {
            return;
        }

        if (!ViewModel.ToggleTopic(topic))
        {
            toggle.IsChecked = false;
            GoalsInfo.Message = Ui.T("onboarding.limit.topics");
            GoalsInfo.IsOpen = true;
        }
        else
        {
            GoalsInfo.IsOpen = false;
        }

        UpdateButtons();
    }

    private void OnSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: HabitTemplate template } card)
        {
            return;
        }

        if (!ViewModel.TogglePick(template))
        {
            card.IsChecked = false;
            HabitsInfo.Message = Ui.T("onboarding.limit.picks");
            HabitsInfo.IsOpen = true;
        }
        else
        {
            HabitsInfo.IsOpen = false;
            RebuildPicked();
        }

        UpdateButtons();
    }

    private void OnCustomAddClick(object sender, RoutedEventArgs e) => AddCustom();

    private void OnCustomKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            AddCustom();
            e.Handled = true;
        }
    }

    private void AddCustom()
    {
        if (ViewModel.AddCustom(CustomBox.Text))
        {
            CustomBox.Text = string.Empty;
            HabitsInfo.IsOpen = false;
            RebuildPicked();
        }
        else if (ViewModel.Picked.Count >= OnboardingViewModel.MaxPicks)
        {
            HabitsInfo.Message = Ui.T("onboarding.limit.picks");
            HabitsInfo.IsOpen = true;
        }

        UpdateButtons();
    }

    private void OnBuddyNameChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncing)
        {
            ViewModel.BuddyName = BuddyNameBox.Text;
            UpdateButtons();
        }
    }

    private void OnBuddyColorSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && BuddyColorGrid.SelectedItem is Ellipse { Tag: string hex })
        {
            ViewModel.BuddyColor = hex;
            BuddyBubble.Background = BuddyBrush(hex);
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush BuddyBrush(string hex)
    {
        try
        {
            return Ui.GradientBrush(HabitGradient.From(hex));
        }
        catch (FormatException)
        {
            return Ui.SoftBrush(hex);
        }
    }

    private void OnReminderChanged(object? sender, EventArgs e)
    {
        if (_syncing || ReminderRadio.SelectedIndex < 0)
        {
            return;
        }

        ViewModel.ReminderChoice = (ReminderChoice)ReminderRadio.SelectedIndex;
        CustomTimePicker.Visibility = ViewModel.ReminderChoice == ReminderChoice.Custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCustomTimeChanged(object sender, TimePickerValueChangedEventArgs e)
    {
        if (!_syncing)
        {
            ViewModel.CustomReminderTime = TimeOnly.FromTimeSpan(e.NewTime);
        }
    }

    /// <summary>Mọi handler async phải bắt lỗi: một lỗi ở đây không được làm app thoát.</summary>
    private static async Task Guard(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Onboarding lỗi", ex);
        }
    }
}
