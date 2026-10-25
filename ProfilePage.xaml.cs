
using AzurChat.Services;

namespace AzurChat.Pages;

public partial class ProfilePage : ContentPage, IQueryAttributable
{
    private readonly IAuthService _authService;
    private string _field = "";

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private string _lastSavedValue = "";
    private bool _isLeaving;
    private bool _allowBack;

    
    public ProfilePage(IAuthService authService, string field = "")
    {
        InitializeComponent();

        _authService = authService;
        _field = field;

        if (!string.IsNullOrEmpty(field))
        {
            InitializeField(
                field,
                field == "signature"
                    ? _authService.CurrentUser?.Signature ?? ""
                    : ""
            );
        }
    }


    
    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        // 没有传 field 时，不覆盖构造函数已有的字段
        if (!query.TryGetValue("field", out var fieldObj))
            return;

        string field = fieldObj?.ToString() ?? "";

        string value = query.TryGetValue("value", out var valueObj)
            ? valueObj?.ToString() ?? ""
            : "";

        InitializeField(field, value);
    }


    private void InitializeField(string field, string value)
    {
        _field = field;

        Title = field switch
        {
            "nickname" => "修改昵称",
            "signature" => "修改签名",
            "region" => "修改地区",
            "birthday" => "修改生日",
            _ => "编辑资料"
        };

        ValueEntry.Text = value;
        ValueEntry.Placeholder = field switch
        {
            "signature" => "指挥官，请写下你的誓言吧！",
            "nickname" => "请输入昵称",
            "region" => "请输入地区",
            "birthday" => "请输入生日",
            _ => "请输入内容"
        };

        _lastSavedValue = value.Trim();
    }

    private async Task SaveAsync()
    {
        string value = ValueEntry.Text?.Trim() ?? "";

        await _saveLock.WaitAsync();

        try
        {
            if (value == _lastSavedValue)
                return;

            switch (_field)
            {
                case "signature":
                    await _authService.UpdateSignatureAsync(value);
                    break;

                case "nickname":
                    if (string.IsNullOrWhiteSpace(value))
                        throw new ArgumentException("昵称不能为空");

                    await _authService.UpdateNicknameAsync(value);
                    break;

                case "region":
                    await _authService.UpdateRegionAsync(value);
                    break;

                case "birthday":
                    if (!DateTime.TryParseExact(
                            value,
                            "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime birthday))
                    {
                        throw new ArgumentException("生日格式必须为 yyyy-MM-dd");
                    }

                    await _authService.UpdateBirthdayAsync(birthday);
                    break;

                default:
                    throw new InvalidOperationException("未知的编辑字段");
            }

            _lastSavedValue = value;
        }
        finally
        {
            _saveLock.Release();
        }
    }
    // 点击空白区域，结束输入并保存
    private void OnBlankTapped(
        object sender,
        TappedEventArgs e)
    {
        ValueEntry.Unfocus();
    }

    // 失去焦点自动保存
    private async void OnValueUnfocused(
        object sender,
        FocusEventArgs e)
    {
        await SaveAsync();
    }

    // 返回之前保存
    private async Task SaveAndBackAsync()
    {
        if (_isLeaving)
            return;

        _isLeaving = true;

        try
        {
            await SaveAsync();

            _allowBack = true;

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _allowBack = false;
        }
        finally
        {
            _isLeaving = false;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.Current.Navigating += OnShellNavigating;
    }

    protected override void OnDisappearing()
    {
        Shell.Current.Navigating -= OnShellNavigating;

        base.OnDisappearing();
    }

    private void OnShellNavigating(
        object? sender,
        ShellNavigatingEventArgs e)
    {
        if (_allowBack || _isLeaving)
            return;

        if (e.Source != ShellNavigationSource.Pop)
            return;

        e.Cancel();

        _ = SaveAndBackAsync();
    }
}
