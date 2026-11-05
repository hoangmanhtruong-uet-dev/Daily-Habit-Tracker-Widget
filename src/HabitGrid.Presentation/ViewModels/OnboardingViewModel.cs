using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.Templates;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.ViewModels;

public enum OnboardingStep
{
    Welcome,
    Goals,
    Habits,
    Buddy,
    Reminder
}

public enum ReminderChoice
{
    None,
    Morning,
    Evening,
    Custom
}

/// <summary>Một thói quen đang được chọn ở bước 3, người dùng sửa nhanh tên và mục tiêu.</summary>
public sealed partial class PickedHabit : ObservableObject
{
    public PickedHabit(HabitTemplate? template, string name, string emoji, HabitKind kind, int target, string? unit, TimeOfDay time)
    {
        Template = template;
        _name = name;
        Emoji = emoji;
        Kind = kind;
        _target = target;
        Unit = unit;
        TimeOfDay = time;
    }

    public HabitTemplate? Template { get; }

    public string Emoji { get; }

    public HabitKind Kind { get; }

    public string? Unit { get; }

    public TimeOfDay TimeOfDay { get; }

    public bool HasNumericTarget => Kind is HabitKind.Count or HabitKind.Duration;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _target;
}

public sealed record OnboardingResult(IReadOnlyList<Habit> Created, IReadOnlyList<ValidationError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Luồng làm quen (02-UX-SCREENS §2): chào → chọn chủ đề → chọn thói quen → đặt tên Buddy → nhắc nhở → hoàn tất.
/// Bước "ghim widget" sẽ thêm khi có widget (SPEC-V2-05). Tiến độ được lưu để mở lại giữa chừng.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int MaxTopics = 3;
    public const int MaxPicks = 3;

    private readonly HabitCreationService _creation;
    private readonly IProfileRepository _profile;
    private readonly ISettingsStore _settings;
    private readonly IDayProvider _dayProvider;
    private readonly ILocalizer _localizer;

    public OnboardingViewModel(
        HabitCreationService creation,
        IProfileRepository profile,
        ISettingsStore settings,
        IDayProvider dayProvider,
        ILocalizer localizer)
    {
        _creation = creation ?? throw new ArgumentNullException(nameof(creation));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dayProvider = dayProvider ?? throw new ArgumentNullException(nameof(dayProvider));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

        _buddyColor = HabitCreationService.PresetColors[0];
        _buddyName = localizer["onboarding.buddy.default"];
    }

    public static IReadOnlyList<HabitTopic> AllTopics { get; } = Enum.GetValues<HabitTopic>();

    public static int StepCount => Enum.GetValues<OnboardingStep>().Length;

    public HashSet<HabitTopic> SelectedTopics { get; } = [];

    /// <summary>Mẫu gợi ý theo các chủ đề đã chọn.</summary>
    public ObservableCollection<HabitTemplate> Suggestions { get; } = [];

    public ObservableCollection<PickedHabit> Picked { get; } = [];

    [ObservableProperty]
    private OnboardingStep _step = OnboardingStep.Welcome;

    [ObservableProperty]
    private string _buddyName;

    [ObservableProperty]
    private string _buddyColor;

    [ObservableProperty]
    private ReminderChoice _reminderChoice = ReminderChoice.Evening;

    [ObservableProperty]
    private TimeOnly _customReminderTime = new(19, 30);

    [ObservableProperty]
    private bool _isBusy;

    public int StepIndex => (int)Step;

    public bool CanGoBack => Step != OnboardingStep.Welcome;

    public bool IsLastStep => Step == OnboardingStep.Reminder;

    /// <summary>Bước hiện tại đã đủ điều kiện để đi tiếp chưa.</summary>
    public bool CanGoNext => Step switch
    {
        OnboardingStep.Goals => SelectedTopics.Count >= 1,
        OnboardingStep.Habits => Picked.Count >= 1 && Picked.All(p => !string.IsNullOrWhiteSpace(p.Name)),
        OnboardingStep.Buddy => !string.IsNullOrWhiteSpace(BuddyName),
        _ => true
    };

    partial void OnStepChanged(OnboardingStep value)
    {
        OnPropertyChanged(nameof(StepIndex));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnBuddyNameChanged(string value) => OnPropertyChanged(nameof(CanGoNext));

    // ------------------------------------------------------------------ chọn chủ đề / thói quen

    /// <summary>Bật/tắt chủ đề. Trả về false khi đã đủ <see cref="MaxTopics"/> và đang cố thêm chủ đề mới.</summary>
    public bool ToggleTopic(HabitTopic topic)
    {
        if (!SelectedTopics.Remove(topic))
        {
            if (SelectedTopics.Count >= MaxTopics)
            {
                return false;
            }

            SelectedTopics.Add(topic);
        }

        RefreshSuggestions();
        OnPropertyChanged(nameof(CanGoNext));
        return true;
    }

    private void RefreshSuggestions()
    {
        Suggestions.Clear();
        foreach (var template in HabitTemplates.For(SelectedTopics))
        {
            Suggestions.Add(template);
        }
    }

    public bool IsPicked(HabitTemplate template) => Picked.Any(p => p.Template?.Id == template.Id);

    /// <summary>Chọn/bỏ một mẫu. Trả về false khi đã đủ <see cref="MaxPicks"/>.</summary>
    public bool TogglePick(HabitTemplate template)
    {
        var existing = Picked.FirstOrDefault(p => p.Template?.Id == template.Id);
        if (existing is not null)
        {
            Picked.Remove(existing);
            OnPropertyChanged(nameof(CanGoNext));
            return true;
        }

        if (Picked.Count >= MaxPicks)
        {
            return false;
        }

        var language = _localizer.Language;
        Picked.Add(new PickedHabit(
            template,
            template.Name(language),
            template.Emoji,
            template.Kind,
            template.Target,
            template.Unit(language),
            template.TimeOfDay));

        OnPropertyChanged(nameof(CanGoNext));
        return true;
    }

    /// <summary>Thêm thói quen tự đặt tên (loại có/không).</summary>
    public bool AddCustom(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Picked.Count >= MaxPicks)
        {
            return false;
        }

        Picked.Add(new PickedHabit(null, name.Trim(), "✨", HabitKind.Boolean, 1, null, TimeOfDay.Anytime));
        OnPropertyChanged(nameof(CanGoNext));
        return true;
    }

    public void RemovePicked(PickedHabit habit)
    {
        Picked.Remove(habit);
        OnPropertyChanged(nameof(CanGoNext));
    }

    // ------------------------------------------------------------------ điều hướng

    /// <summary>Sang bước kế. Ở bước cuối thì không làm gì (hãy gọi <see cref="FinishAsync"/>).</summary>
    public async Task<bool> NextAsync()
    {
        if (!CanGoNext || IsLastStep)
        {
            return false;
        }

        Step = (OnboardingStep)((int)Step + 1);
        await SaveStateAsync();
        return true;
    }

    public async Task BackAsync()
    {
        if (!CanGoBack)
        {
            return;
        }

        Step = (OnboardingStep)((int)Step - 1);
        await SaveStateAsync();
    }

    /// <summary>Bỏ qua onboarding (người dùng có dữ liệu cũ hoặc muốn tự làm). Không tạo gì.</summary>
    public async Task SkipAsync()
    {
        await _settings.SetAsync(SettingKeys.OnboardingDone, "1");
        await _settings.RemoveAsync(SettingKeys.OnboardingState);
    }

    // ------------------------------------------------------------------ hoàn tất

    public TimeOnly? ReminderTime => ReminderChoice switch
    {
        ReminderChoice.Morning => new TimeOnly(8, 0),
        ReminderChoice.Evening => new TimeOnly(20, 0),
        ReminderChoice.Custom => CustomReminderTime,
        _ => null
    };

    /// <summary>Tạo các thói quen đã chọn, lưu Buddy, đánh dấu hoàn tất.</summary>
    public async Task<OnboardingResult> FinishAsync()
    {
        if (IsBusy)
        {
            return new OnboardingResult([], []);
        }

        IsBusy = true;

        try
        {
            var today = _dayProvider.GetToday();
            var reminders = ReminderTime is { } time ? new[] { new Reminder(time) } : null;
            var created = new List<Habit>();

            foreach (var pick in Picked)
            {
                var template = pick.Template;
                var draft = new HabitDraft(
                    pick.Name,
                    HabitIcon.Emoji(pick.Emoji),
                    pick.Kind,
                    pick.Target,
                    pick.Unit,
                    pick.TimeOfDay,
                    template?.ToFrequency(today),
                    null,
                    null,
                    reminders);

                var result = await _creation.CreateAsync(draft);
                if (!result.Succeeded)
                {
                    return new OnboardingResult(created, result.Errors);
                }

                created.Add(result.Habit!);
            }

            var profile = await _profile.GetAsync();
            await _profile.SaveAsync(profile with
            {
                BuddyName = BuddyName.Trim(),
                BuddyColor = BuddyColor
            });

            await _settings.SetAsync(SettingKeys.OnboardingDone, "1");
            await _settings.RemoveAsync(SettingKeys.OnboardingState);
            return new OnboardingResult(created, []);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------ lưu/khôi phục tiến độ

    private sealed record PickedState(string? TemplateId, string Name, int Target);

    private sealed record State(
        int Step,
        List<string> Topics,
        List<PickedState> Picks,
        string BuddyName,
        string BuddyColor,
        string Reminder,
        string CustomTime);

    private async Task SaveStateAsync()
    {
        var state = new State(
            (int)Step,
            SelectedTopics.Select(t => t.ToString()).ToList(),
            Picked.Select(p => new PickedState(p.Template?.Id, p.Name, p.Target)).ToList(),
            BuddyName,
            BuddyColor,
            ReminderChoice.ToString(),
            CustomReminderTime.ToString("HH:mm"));

        await _settings.SetAsync(SettingKeys.OnboardingState, JsonSerializer.Serialize(state, DomainJson.Options));
    }

    /// <summary>Khôi phục tiến độ đã lưu (nếu có). Dữ liệu hỏng thì bắt đầu lại từ đầu.</summary>
    public async Task RestoreAsync()
    {
        var raw = await _settings.GetAsync(SettingKeys.OnboardingState);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<State>(raw, DomainJson.Options);
            if (state is null)
            {
                return;
            }

            SelectedTopics.Clear();
            foreach (var name in state.Topics)
            {
                if (Enum.TryParse<HabitTopic>(name, out var topic))
                {
                    SelectedTopics.Add(topic);
                }
            }

            RefreshSuggestions();

            Picked.Clear();
            var language = _localizer.Language;
            foreach (var pick in state.Picks)
            {
                var template = pick.TemplateId is null ? null : HabitTemplates.Find(pick.TemplateId);
                Picked.Add(template is null
                    ? new PickedHabit(null, pick.Name, "✨", HabitKind.Boolean, 1, null, TimeOfDay.Anytime)
                    : new PickedHabit(template, pick.Name, template.Emoji, template.Kind, pick.Target, template.Unit(language), template.TimeOfDay));
            }

            BuddyName = state.BuddyName;
            BuddyColor = state.BuddyColor;
            ReminderChoice = Enum.TryParse<ReminderChoice>(state.Reminder, out var choice) ? choice : ReminderChoice.Evening;
            CustomReminderTime = TimeOnly.TryParse(state.CustomTime, out var custom) ? custom : CustomReminderTime;
            Step = Enum.IsDefined((OnboardingStep)state.Step) ? (OnboardingStep)state.Step : OnboardingStep.Welcome;
            OnPropertyChanged(nameof(CanGoNext));
        }
        catch (JsonException)
        {
            await _settings.RemoveAsync(SettingKeys.OnboardingState);
        }
    }

    /// <summary>Cần chạy onboarding khi chưa hoàn tất và chưa có thói quen nào (ví dụ người dùng đã nhập từ v1 thì bỏ qua).</summary>
    public static async Task<bool> IsNeededAsync(ISettingsStore settings, IHabitRepository habits)
    {
        if (await settings.GetAsync(SettingKeys.OnboardingDone) == "1")
        {
            return false;
        }

        if ((await habits.GetAllAsync()).Count > 0)
        {
            await settings.SetAsync(SettingKeys.OnboardingDone, "1");
            return false;
        }

        return true;
    }
}
