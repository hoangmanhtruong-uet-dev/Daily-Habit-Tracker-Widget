using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;
using HabitGrid.UI.Views;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử luồng làm quen ban đầu (SPEC-04 mục 5).
/// </summary>
public class OnboardingTests : IAsyncLifetime
{
    private HabitGridDatabase _database = null!;
    private IHabitRepository _habits = null!;

    private static readonly DateOnly Today = new(2026, 10, 22);

    public async Task InitializeAsync()
    {
        _database = HabitGridDatabase.CreateInMemory();
        await _database.InitializeAsync();

        _habits = new HabitRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public void Samples_ShouldContainFiveSuggestionsAsSpecified()
    {
        OnboardingWindow.Samples.Should().HaveCount(5);

        OnboardingWindow.Samples.Should().Contain(s => s.Name == "Uống đủ 2L nước" && s.Kind == HabitKind.Count && s.Target == 8);
        OnboardingWindow.Samples.Should().Contain(s => s.Name == "Đọc sách 20 phút" && s.Kind == HabitKind.Boolean);
        OnboardingWindow.Samples.Should().Contain(s => s.Name == "Tập thể dục / Đi bộ");
        OnboardingWindow.Samples.Should().Contain(s => s.Name == "Học lập trình / Kỹ năng mới");
        OnboardingWindow.Samples.Should().Contain(s => s.Name == "Ngồi thiền 10 phút");
    }

    [Fact]
    public void Samples_ShouldUseValidIconKeys()
    {
        foreach (var sample in OnboardingWindow.Samples)
        {
            Theme.IconCatalog.Get(sample.IconKey).Key.Should().Be(sample.IconKey,
                $"icon '{sample.IconKey}' phải tồn tại trong catalog");
        }
    }

    [Fact]
    public async Task CreatingHabitFromSample_ShouldPersistImmediately()
    {
        var sample = OnboardingWindow.Samples[0];

        var habit = new Habit
        {
            Name = sample.Name,
            Icon = sample.IconKey,
            Kind = sample.Kind,
            Target = sample.Target,
            StartDate = Today
        };

        await _habits.InsertAsync(habit);

        var stored = await _habits.GetByIdAsync(habit.Id);

        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Uống đủ 2L nước");
        stored.Kind.Should().Be(HabitKind.Count);
        stored.Target.Should().Be(8);
    }
}
