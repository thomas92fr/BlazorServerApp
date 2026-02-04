using System.Reflection;
using Model.Entities;
using Model.UnitOfWork;
using Model.ViewModels;

namespace Model.Factories;

/// <summary>
/// Generic default factory that creates ViewModels using reflection.
/// Looks for a constructor with signature (TEntity, IUnitOfWork) or (TEntity, IUnitOfWork, ILogger?).
/// Use this as fallback when no custom factory is defined.
/// </summary>
public class DefaultEntityViewModelFactory<TEntity, TViewModel> : IEntityViewModelFactory<TEntity, TViewModel>
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    private readonly ConstructorInfo _constructor;
    private readonly int _parameterCount;

    public DefaultEntityViewModelFactory()
    {
        var vmType = typeof(TViewModel);

        // Try to find constructor: (TEntity, IUnitOfWork, ILogger?) - 3 params with optional logger
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
                $"Expected constructor({typeof(TEntity).Name}, IUnitOfWork) or " +
                $"constructor({typeof(TEntity).Name}, IUnitOfWork, ILogger?).");

        _parameterCount = _constructor.GetParameters().Length;
    }

    public TViewModel Create(TEntity entity, IUnitOfWork unitOfWork)
    {
        var args = _parameterCount == 2
            ? new object[] { entity, unitOfWork }
            : new object?[] { entity, unitOfWork, null }; // null for optional ILogger

        return (TViewModel)_constructor.Invoke(args);
    }
}
