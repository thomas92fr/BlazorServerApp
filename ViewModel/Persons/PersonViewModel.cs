using FluentValidation;
using Model.Factories;
using Model.Repositories;
using Model.ViewModels;
using Microsoft.Extensions.Logging;
using Model.Entities;
using ViewModel.Commons.Bases;
using ViewModel.Commons.Fields;

namespace ViewModel.Persons;

/// <summary>
/// Factory for creating PersonViewModel instances.
/// CONVENTION: Must be named {EntityName}ViewModelFactory in same namespace as ViewModel.
/// </summary>
public class PersonViewModelFactory : IEntityViewModelFactory<Person, PersonViewModel>
{
    public PersonViewModel Create(Person entity, IRepository repository)
    {
        return new PersonViewModel(entity, repository);
    }
}


/// <summary>
/// ViewModel for Person entity.
/// Demonstrates full FieldViewModel pattern with validation.
///
/// BLAZOR USAGE:
/// 1. Get from Repository: Repository.GetViewModel<Model.Entities.Person, PersonViewModel>(person)
/// 2. Bind to properties: @bind="ViewModel.Name.Value"
/// 3. Display validation: @ViewModel.Name.Error
/// </summary>
public partial class PersonViewModel : BaseViewModel, IEntityViewModel<Model.Entities.Person>
{
    private readonly Model.Entities.Person _person;

    // Field ViewModels (lazy-initialized with ??= operator)
    private IntegerFieldViewModel? _idField;
    private StringFieldViewModel? _nameField;
    private IntegerFieldViewModel? _ageField;
    private BoolFieldViewModel? _isTeacherField;
    private DateTimeFieldViewModel? _startDateTimeField;
    private DateTimeFieldViewModel? _endDateTimeField;
    private IntegerFieldViewModel? _durationInDaysField;

    public PersonViewModel(
        Model.Entities.Person person,
        IRepository repository,
        ILogger<PersonViewModel>? logger = null
    ) : base(repository, logger)
    {
        _person = person;
    }

    public Model.Entities.Person Model => _person;

    /// <summary>
    /// Id property (read-only).
    /// </summary>
    public IntegerFieldViewModel Id => _idField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => _person.Id,
        setValue: value => _person.Id = value)
    {
        Label = "Id",
        Hint = "Person ID in database",
        ReadOnly = true
    };

    /// <summary>
    /// Name property with validation.
    /// BLAZOR EXAMPLE:
    /// <InputText @bind-Value="ViewModel.Name.Value" class="form-control" />
    /// <span class="text-danger">@ViewModel.Name.Error</span>
    /// <span class="text-warning">@ViewModel.Name.Warning</span>
    /// </summary>
    public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
        parent: this,
        getValue: () => _person.Name,
        setValue: value => _person.Name = value)
    {
        Label = "Name",
        Hint = "Person's full name",
        ValidationRules = rules => rules
            .NotEmpty().WithMessage("Name is required.")
                .WithSeverity(Severity.Error)
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.")
                .WithSeverity(Severity.Error)
            .Must(BeAValidName!).WithMessage("Name contains invalid characters.")
                .WithSeverity(Severity.Error)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Length >= 2)
                .WithMessage("Very short names may be invalid.")
                .WithSeverity(Severity.Warning)
    };

    /// <summary>
    /// Custom validation method for Name field.
    /// </summary>
    private bool BeAValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        return name.All(c => char.IsLetter(c) || c == ' ' || c == '-');
    }

    /// <summary>
    /// Age property with Error and Warning validations.
    /// </summary>
    public IntegerFieldViewModel Age => _ageField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => _person.Age,
        setValue: value => _person.Age = value)
    {
        Label = "Age",
        Hint = "Person's age in years",
        ValidationRules = rules => rules
            // ERRORS (block save)
            .GreaterThanOrEqualTo(0).WithMessage("Age cannot be negative.")
                .WithSeverity(Severity.Error)
            .LessThan(150).WithMessage("Age must be less than 150 years.")
                .WithSeverity(Severity.Error)
            // WARNINGS (don't block save)
            .Must(age => age < 100).WithMessage("An age over 100 is unusual.")
                .WithSeverity(Severity.Warning)
            .Must(age => age >= 18).WithMessage("This person is a minor.")
                .WithSeverity(Severity.Warning)
    };

    /// <summary>
    /// Boolean property (checkbox in UI).
    /// BLAZOR: <InputCheckbox @bind-Value="ViewModel.IsTeacher.Value" />
    /// </summary>
    public BoolFieldViewModel IsTeacher => _isTeacherField ??= new BoolFieldViewModel(
        parent: this,
        getValue: () => _person.IsTeacher,
        setValue: value => _person.IsTeacher = value)
    {
        Label = "Is Teacher",
        Hint = "Check if person is a teacher"
    };

    /// <summary>
    /// DateTime property (date picker in UI).
    /// BLAZOR: <InputDate @bind-Value="ViewModel.StartDateTime.Value" />
    /// </summary>
    public DateTimeFieldViewModel StartDateTime => _startDateTimeField ??= new DateTimeFieldViewModel(
        parent: this,
        getValue: () => _person.StartDateTime,
        setValue: value => _person.StartDateTime = value)
    {
        Label = "Start Date",
        Hint = "Start date and time",
        NotifyOnChange = new[] { nameof(DurationInDays) }
    };

    public DateTimeFieldViewModel EndDateTime => _endDateTimeField ??= new DateTimeFieldViewModel(
        parent: this,
        getValue: () => _person.EndDateTime,
        setValue: value => _person.EndDateTime = value)
    {
        Label = "End Date",
        Hint = "End date and time",
        NotifyOnChange = new[] { nameof(DurationInDays) }
    };

    /// <summary>
    /// Computed field: Number of days between StartDateTime and EndDateTime.
    /// Recalculates automatically when StartDateTime or EndDateTime changes.
    /// BLAZOR USAGE: Display as read-only field, updates automatically.
    /// </summary>
    public IntegerFieldViewModel DurationInDays => _durationInDaysField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => (int)(EndDateTime.Value - StartDateTime.Value).TotalDays,
        setValue: null)
    {
        Label = "Duration (Days)",
        Hint = "Calculated from Start and End dates",
        IsComputed = true, // ReadOnly is automatically true for computed fields
        ValidationRules = rules => rules
            .GreaterThanOrEqualTo(0)
                .WithMessage("Duration must be positive (End date must be after Start date).")
                .WithSeverity(Severity.Error)
            .LessThan(36500)
                .WithMessage("Duration exceeds 100 years - please verify dates.")
                .WithSeverity(Severity.Warning)
    };

    public override string ToString() => $"{Name.Value} ({Id.Value})";
}
