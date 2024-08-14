﻿// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;

using Autofac.Util;

namespace Autofac.Core.Activators.Reflection;

/// <summary>
/// Holds an instance of a known property on a type instantiated by the <see cref="ReflectionActivator"/>.
/// </summary>
internal class InjectableProperty
{
    private readonly MethodInfo _setter;
    private readonly ParameterInfo _setterParameter;

    /// <summary>
    /// Initializes a new instance of the <see cref="InjectableProperty"/> class.
    /// </summary>
    /// <param name="prop">The property info.</param>
    public InjectableProperty(PropertyInfo prop)
    {
        Property = prop;

        _setter = prop.SetMethod!;

        _setterParameter = _setter.GetParameters()[0];

        IsRequired = prop.HasRequiredMemberAttribute();
    }

    /// <summary>
    /// Gets the underlying property.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Gets a value indicating whether this field is marked as required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Try and supply a value for this property using the given parameter.
    /// </summary>
    /// <param name="instance">The object instance.</param>
    /// <param name="p">The parameter that may provide the value.</param>
    /// <param name="context">The component context.</param>
    /// <returns>True if the parameter could provide a value, and the property was set. False otherwise.</returns>
    public bool TrySupplyValue(object instance, Parameter p, IComponentContext context)
    {
        if (p.CanSupplyValue(_setterParameter, context, out var vp))
        {
            Property.SetValue(instance, vp(), null);

            return true;
        }

        return false;
    }
}
