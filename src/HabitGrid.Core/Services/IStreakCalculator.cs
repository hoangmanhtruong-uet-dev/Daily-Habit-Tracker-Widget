using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Kết quả tính toán chuỗi thói quen.
/// </summary>
/// <param name="CurrentStreak">Chuỗi hiện tại (số ngày có lịch hoàn thành liên tiếp tính lùi từ hôm nay).</param>
/// <param name="LongestStreak">Chuỗi dài nhất trong toàn bộ lịch sử.</param>
/// <param name="CompletionRate30Days">Tỷ lệ hoàn thành trong 30 ngày gần nhất (0.0 – 1.0).</param>
/// <param name="TotalCompletedDays">Tổng số ngày đã hoàn thành, gồm cả các ngày ngoài lịch.</param>
public record StreakResult(
    int CurrentStreak,
    int LongestStreak,
    double CompletionRate30Days,
    int TotalCompletedDays);

/// <summary>
/// Thuật toán tính chuỗi (streak) của một thói quen.
/// </summary>
public interface IStreakCalculator
{
    StreakResult Calculate(Habit habit, IReadOnlyList<Entry> entries, DateOnly today);
}
