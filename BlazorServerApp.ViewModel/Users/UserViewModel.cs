using BlazorServerApp.Model.Entities;
using BlazorServerApp.Model.ViewModels;
using BlazorServerApp.ViewModel.Commons.Bases;
using BlazorServerApp.ViewModel.Commons.Fields;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BlazorServerApp.ViewModel.Users;

/// <summary>
/// ViewModel for the User entity.
/// </summary>
public class UserViewModel : BaseViewModel, IEntityViewModel<User>
{
    private readonly User _user;

    private IntegerFieldViewModel? _idField;
    private StringFieldViewModel? _nameField;
    private PasswordFieldViewModel? _passwordField;

    public UserViewModel(User user, IRootViewModel rootViewModel, ILogger<UserViewModel>? logger = null)
        : base(rootViewModel, logger)
    {
        _user = user;
    }

    public User Model => _user;

    public IntegerFieldViewModel Id => _idField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => _user.Id,
        setValue: value => _user.Id = value)
    {
        Label = "Id",
        Hint = "User ID in database",
        ReadOnly = true,
        ColumnWidth = "60px",
        ColumnOrder = 1
    };

    public StringFieldViewModel UserName => _nameField ??= new StringFieldViewModel(
        parent: this,
        getValue: () => _user.UserName,
        setValue: value => _user.UserName = value)
    {
        Label = "User Name",
        Hint = "Unique username for login",
        ColumnOrder = 2,
        ValidationRules = rules => rules
            .NotEmpty().WithMessage("User name is required.").WithSeverity(Severity.Error)
            .MaximumLength(100).WithMessage("User name cannot exceed 100 characters.").WithSeverity(Severity.Error)
    };

    public PasswordFieldViewModel Password => _passwordField ??= new PasswordFieldViewModel(
        parent: this,
        getValue: () => _user.Password,
        setValue: value => _user.Password = BCrypt.Net.BCrypt.HashPassword(value))
    {
        Label = "Password",
        Hint = "User password",
        ColumnOrder = 3,
        HiddenInColumn = true,
        ValidationRules = rules => rules
            .NotEmpty().WithMessage("Password is required.").WithSeverity(Severity.Error)
    };

    public override string ToString() => $"{UserName.Value} ({Id.Value})";
}
