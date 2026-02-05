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
/// Receives IRootViewModel for access to tab context and Save/Discard commands.
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
    private DecimalFieldViewModel? _scoreField;
    private TimeSpanFieldViewModel? _workDurationField;

    public PersonViewModel(
        Person person,
        IRootViewModel rootViewModel,
        ILogger<PersonViewModel>? logger = null
    ) : base(rootViewModel, logger)
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
        ReadOnly = true,
        ColumnWidth = "60px",
        ColumnOrder = 1,
        FormGroupHeader = "Identification",
        FormGroupOrder = 1
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
        ColumnOrder = 2,
        FormGroupHeader = "Identification",
        FormGroupOrder = 1,
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
        ColumnWidth = "80px",
        ColumnOrder = 3,
        FormGroupHeader = "Informations personnelles",
        FormGroupOrder = 2,
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
        Hint = "Check if person is a teacher",
        ColumnOrder = 4,
        FormGroupHeader = "Informations personnelles",
        FormGroupOrder = 2,
        HiddenInColumn = true
    };

    /// <summary>
    /// Decimal property demonstrating DecimalFieldViewModel with formatting.
    /// </summary>
    public DecimalFieldViewModel Score => _scoreField ??= new DecimalFieldViewModel(
        parent: this,
        getValue: () => _person.Score,
        setValue: value => _person.Score = value)
    {
        Label = "Score",
        Hint = "Person's evaluation score (0-100)",
        ColumnOrder = 5,
        FormGroupHeader = "Informations personnelles",
        FormGroupOrder = 2,
        Format = "#.00",
        Step = 0.5m,
        Min = 0m,
        Max = 100m,
        ValidationRules = rules => rules
            .GreaterThanOrEqualTo(0m).WithMessage("Score cannot be negative.")
                .WithSeverity(Severity.Error)
            .LessThanOrEqualTo(100m).WithMessage("Score cannot exceed 100.")
                .WithSeverity(Severity.Error)
            .Must(score => score >= 50m).WithMessage("Score below 50 is a failing grade.")
                .WithSeverity(Severity.Warning)
    };

    /// <summary>
    /// TimeSpan property demonstrating TimeSpanFieldViewModel.
    /// </summary>
    public TimeSpanFieldViewModel WorkDuration => _workDurationField ??= new TimeSpanFieldViewModel(
        parent: this,
        getValue: () => _person.WorkDuration,
        setValue: value => _person.WorkDuration = value)
    {
        Label = "Work Duration",
        Hint = "Daily work duration",
        ColumnOrder = 6,
        FormGroupHeader = "Informations personnelles",
        FormGroupOrder = 2,
        ShowDays = false,
        ShowSeconds = false,
        ValidationRules = rules => rules
            .LessThanOrEqualTo(TimeSpan.FromHours(24)).WithMessage("Duration cannot exceed 24 hours.")
                .WithSeverity(Severity.Error)
            .Must(d => d <= TimeSpan.FromHours(10)).WithMessage("Working more than 10 hours is not recommended.")
                .WithSeverity(Severity.Warning)
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
        ColumnOrder = 7,
        FormGroupHeader = "Période",
        FormGroupOrder = 3,
        NotifyOnChange = new[] { nameof(DurationInDays) },
        HiddenInColumn = true
    };

    public DateTimeFieldViewModel EndDateTime => _endDateTimeField ??= new DateTimeFieldViewModel(
        parent: this,
        getValue: () => _person.EndDateTime,
        setValue: value => _person.EndDateTime = value)
    {
        Label = "End Date",
        Hint = "End date and time",
        ColumnOrder = 8,
        FormGroupHeader = "Période",
        FormGroupOrder = 3,
        NotifyOnChange = new[] { nameof(DurationInDays) },
        HiddenInColumn = true
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
        ColumnOrder = 9,
        FormGroupHeader = "Encadrement",
        FormGroupOrder = 4,
        HiddenInColumn = true,
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
        ColumnOrder = 10,
        FormGroupHeader = "Période",
        FormGroupOrder = 3,
        IsComputed = true,
        HiddenInColumn = true,
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
