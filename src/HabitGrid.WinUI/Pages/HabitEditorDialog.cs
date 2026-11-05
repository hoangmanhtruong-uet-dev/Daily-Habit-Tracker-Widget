using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI.Pages;

/// <summary>Hộp thoại tạo/sửa thói quen đầy đủ (thay hộp thoại thêm nhanh trước đó).</summary>
public static class HabitEditorDialog
{
    public static HabitEditorView CreateView(Habit? existing = null)
    {
        var editor = App.Host.Get<HabitEditorViewModel>();

        if (existing is not null)
        {
            editor.LoadFrom(existing);
        }

        return new HabitEditorView(editor);
    }

    /// <summary>Trả về thói quen đã lưu, hoặc null nếu người dùng hủy.</summary>
    public static async Task<Habit?> ShowAsync(XamlRoot xamlRoot, Habit? existing = null)
    {
        var view = CreateView(existing);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Ui.T(existing is null ? "editor.title.new" : "editor.title.edit"),
            Content = view,
            PrimaryButtonText = Ui.T("editor.save"),
            CloseButtonText = Ui.T("editor.cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();

            try
            {
                // Giữ hộp thoại mở và nói rõ lỗi khi chưa hợp lệ.
                if (!await view.TrySaveAsync())
                {
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return view.Saved;
    }
}
