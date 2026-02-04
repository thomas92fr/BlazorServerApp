using FluentValidation;
using Model.UnitOfWork;
using Model.ViewModels;
using Microsoft.Extensions.Logging;
using Model.Entities;
using ViewModel.Commons.Bases;
using ViewModel.Commons.Fields;

namespace ViewModel.Persons;

/// <summary>
/// ViewModel for Person entity.
/// Demonstrates full FieldViewModel pattern with validation.
/// </summary>
public partial class PersonViewModel : BaseViewModel, IEntityViewModel<Person>
{
    private readonly Person _person;

    // Field ViewModels (lazy-initialized with ??= operator)
    private IntegerFieldViewModel? _idField;
    private StringFieldViewModel? _nameField;
    private IntegerFieldViewModel? _ageField;
    private BoolFieldViewModel? _isTeacherField;
    private DateTimeFieldViewModel? _startDateTimeField;
    private DateTimeFieldViewModel? _endDateTimeField;
    private ReferenceFieldViewModel<PersonViewModel>? _mentorField;
    private IntegerFieldViewModel? _durationInDaysField;

    public PersonViewModel(
        Person person,
        IUnitOfWork unitOfWork,
        ILogger<PersonViewModel>? logger = null
    ) : base(unitOfWork, logger)
    {
        _person = person;
    }

    public Person Model => _person;

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
    /// Age property with dropdown list selection.
    /// </summary>
    public IntegerFieldViewModel Age => _ageField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => _person.Age,
        setValue: value => _person.Age = value,
        listQuery: () => Enumerable.Range(18, 83).ToList()) // 18 to 100
    {
        Label = "Age",
        Hint = "Person's age in years",
        ValueMustBeInTheList = false,
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
            .Must(age => age < 65).WithMessage("This person is at retirement age.")
                .WithSeverity(Severity.Warning)
    };

    /// <summary>
    /// Boolean property (checkbox in UI).
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
    /// Reference to another Person acting as mentor (nullable).
    /// Value is a PersonViewModel; setValue syncs back the entity and FK.
    /// </summary>
    public ReferenceFieldViewModel<PersonViewModel> Mentor => _mentorField ??= new ReferenceFieldViewModel<PersonViewModel>(
        parent: this,
        getValue: () => UnitOfWork.GetViewModel<Person, PersonViewModel>(_person.Mentor),
        setValue: value => _person.Mentor = value?.Model,
        listQuery: () => UnitOfWork.GetAllViewModels<Person, PersonViewModel>().ToList()
    )
    {
        Label = "Mentor",
        Hint = "Select a mentor",
        ValidationRules = rules => rules
            .Must(mentor => mentor?.Model != _person)
                .WithMessage("Une personne ne peut pas être son propre mentor.")
                .WithSeverity(Severity.Error)
    };

    /// <summary>
    /// Computed field: Number of days between StartDateTime and EndDateTime.
    /// Recalculates automatically when StartDateTime or EndDateTime changes.
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
