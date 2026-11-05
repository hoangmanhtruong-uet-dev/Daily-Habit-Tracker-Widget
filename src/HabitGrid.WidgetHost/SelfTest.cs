using System.IO;
using HabitGrid.Core.Domain;
using HabitGrid.Data;
using HabitGrid.Data.V3;

namespace HabitGrid.WidgetHost;

/// <summary>
/// Tự kiểm tra đầu-cuối (CI): có widget dựng từ CSDL, bấm ô hôm nay ghi vào CSDL, và một ghi từ kết nối khác
/// (như app chính) hiện lên widget trong vài giây. Trả 0 nếu đạt, 2 nếu hỏng.
/// </summary>
public static class SelfTest
{
    public static async Task<int> RunAsync(WidgetController controller, string dbPath)
    {
        try
        {
            await Task.Delay(1500);
            Require(controller.Windows.Count > 0, "Không dựng được widget nào từ CSDL.");
            Require(controller.Windows.All(w => w.IsVisible), "Có widget không hiển thị.");

            var model = controller.Models.First();
            var habitId = model.Habit.Id;
            var today = controller.Today;
            var before = model.Values.GetValueOrDefault(today);

            // 1) Bấm ô hôm nay trên widget: CSDL phải đổi và widget phải cập nhật.
            model.RequestMark(today);
            await Task.Delay(1500);

            using (var db = HabitGridV3.Open(dbPath))
            {
                var logs = new DayLogRepository(db, new HlcClock("selftest"), null);
                var log = await logs.GetAsync(habitId, today);
                Require((log?.Value ?? 0) != before, "Bấm widget nhưng CSDL không đổi.");
            }

            Require(model.Values.GetValueOrDefault(today) != before, "Widget không cập nhật sau khi check-in.");

            // 2) Ghi từ kết nối khác (như app chính): widget phải thấy qua data_version.
            var yesterday = today.AddDays(-1);

            using (var db = HabitGridV3.Open(dbPath))
            {
                var clock = new HlcClock("selftest2");
                var habit = await new HabitRepository(db, clock, null).GetByIdAsync(habitId);
                var logs = new DayLogRepository(db, clock, null);
                await logs.UpsertAsync(DayLogRules.Normalize(habit!, new DayLog { HabitId = habitId, Day = yesterday, Value = 1 }));
            }

            await Task.Delay(4500);
            Require(model.Values.ContainsKey(yesterday), "Ghi từ kết nối khác không hiện lên widget (làm mới theo data_version hỏng).");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELFTEST FAILED: " + ex.Message);
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(dbPath)!, "widgethost-selftest.log"), ex + "\n");
            return 2;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
