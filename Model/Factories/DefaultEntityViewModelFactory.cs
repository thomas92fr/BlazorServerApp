using System.Reflection;
using Model.Entities;
using Model.UnitOfWork;
using Model.ViewModels;

namespace Model.Factories;

/// <summary>
/// Generic default factory that creates ViewModels using reflection.
/// Supports both new pattern (TEntity, IRootViewModel, ILogger?) and
/// legacy pattern (TEntity, IUnitOfWork, ILogger?).
/// Use this as fallback when no custom factory is defined.
/// </summary>
public class DefaultEntityViewModelFactory<TEntity, TViewModel> : IEntityViewModelFactory<TEntity, TViewModel>
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    private readonly ConstructorInfo _constructor;
    private readonly int _parameterCount;
    private readonly ConstructorParameterType _parameterType;

    private enum ConstructorParameterType
    {
        RootViewModel,  // New: (TEntity, IRootViewModel, ILogger?)
        UnitOfWork      // Legacy: (TEntity, IUnitOfWork, ILogger?)
    }

    public DefaultEntityViewModelFactory()
    {
        var vmType = typeof(TViewModel);

        // First, try to find constructor with IRootViewModel (new pattern)
        _constructor = vmType.GetConstructors()
            .Where(c => c.GetParameters().Length >= 2 && c.GetParameters().Length <= 3)
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return typeof(TEntity).IsAssignableFrom(parameters[0].ParameterType)
                    && typeof(IRootViewModel).IsAssignableFrom(parameters[1].ParameterType);
            })!;

        if (_constructor != null)
        {
            _parameterType = ConstructorParameterType.RootViewModel;
            _parameterCount = _constructor.GetParameters().Length;
            return;
        }

        // Fallback: constructor with IUnitOfWork (legacy pattern)
        _constructor = vmType.GetConstructors()
            .Where(c => c.GetParameters().Length >= 2 && c.GetParameters().Length <= 3)
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return typeof(TEntity).IsAssignableFrom(parameters[0].ParameterType)
                    && typeof(IUnitOfWork).IsAssignableFrom(parameters[1].ParameterType);
            })
            ?? throw new InvalidOperationException(
                $"No suitable constructor found for {vmType.Name}. " +
                $"Expected constructor({typeof(TEntity).Name}, IRootViewModel[, ILogger?]) or " +
                $"constructor({typeof(TEntity).Name}, IUnitOfWork[, ILogger?]).");

        _parameterType = ConstructorParameterType.UnitOfWork;
        _parameterCount = _constructor.GetParameters().Length;
    }

    /// <summary>
    /// Creates a ViewModel with access to the root ViewModel (preferred).
    /// </summary>
    public TViewModel Create(TEntity entity, IRootViewModel rootViewModel)
    {
        object?[] args;

        if (_parameterType == ConstructorParameterType.RootViewModel)
        {
            // ViewModel expects IRootViewModel
            args = _parameterCount == 2
                ? new object[] { entity, rootViewModel }
                : new object?[] { entity, rootViewModel, null };
        }
        else
        {
            // ViewModel expects IUnitOfWork (legacy) - extract from rootViewModel
            args = _parameterCount == 2
                ? new object[] { entity, rootViewModel.UnitOfWork }
                : new object?[] { entity, rootViewModel.UnitOfWork, null };
        }

        return (TViewModel)_constructor.Invoke(args);
    }

    /// <summary>
    /// Legacy method: Creates a ViewModel with direct UnitOfWork access.
    /// </summary>
    public TViewModel Create(TEntity entity, IUnitOfWork unitOfWork)
    {
        if (_parameterType == ConstructorParameterType.RootViewModel)
        {
            throw new InvalidOperationException(
                $"{typeof(TViewModel).Name} requires IRootViewModel, not IUnitOfWork. " +
                $"Use Create(entity, rootViewModel) instead.");
        }

        var args = _parameterCount == 2
            ? new object[] { entity, unitOfWork }
            : new object?[] { entity, unitOfWork, null };

        return (TViewModel)_constructor.Invoke(args);
    }
}
