﻿// This software is part of the Autofac IoC container
// Copyright (c) 2007 Nicholas Blumhardt
// nicholas.blumhardt@gmail.com
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac.Builder;
using Autofac.Component;

namespace Autofac.Registrars.Collection
{
    /// <summary>
    /// Exposed by the generic ServiceListRegistration type to expose non-generic Add().
    /// </summary>
    interface IServiceListRegistration
    {
        /// <summary>
        /// Add a service (key into another component registration) to those returned
        /// in the list.
        /// </summary>
        /// <param name="item"></param>
        void Add(Service item);
    }

    /// <summary>
    /// Registration that exposes collection interfaces onto a subset of other components
    /// in the container.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    class ServiceListRegistration<TItem> : Registration, IServiceListRegistration
    {
        #region Inner classes

        /// <summary>
        /// Custom activator that maintains a service list and returns instances
        /// of List.
        /// </summary>
        class ServiceListActivator : IActivator
        {
            IList<Service> _items = new List<Service>();

            /// <summary>
            /// Gets the services that will appear in instances of the list.
            /// </summary>
            /// <value>The items.</value>
            public IList<Service> Items
            {
                get
                {
                    return _items;
                }
            }

            #region IActivator Members

            /// <summary>
            /// Create a component instance, using container
            /// to resolve the instance's dependencies.
            /// </summary>
            /// <param name="context">The context to use
            /// for dependency resolution.</param>
            /// <param name="parameters">Parameters that can be used in the resolution process.</param>
            /// <returns>
            /// A component instance. Note that while the
            /// returned value need not be created on-the-spot, it must
            /// not be returned more than once by consecutive calls. (Throw
            /// an exception if this is attempted. IActivationScope should
            /// manage singleton semantics.)
            /// </returns>
            public object ActivateInstance(IContext context, IActivationParameters parameters)
            {
                Enforce.ArgumentNotNull(context, "context");
                Enforce.ArgumentNotNull(parameters, "parameters");

                var instance = new List<TItem>();
                foreach (var item in _items)
                {
                    object itemInstance = context.Resolve(item);
                    instance.Add((TItem)itemInstance);
                }

                return instance;
            }

            /// <summary>
            /// Not supported as the ServiceListRegistration class overrides
            /// DuplicateForNewContext to avoid this method call.
            /// </summary>
            public bool CanSupportNewContext
            {
                get { throw new InvalidOperationException(); }
            }

            #endregion
        }
        
        #endregion

        ServiceListActivator _activator = new ServiceListActivator();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceListRegistration&lt;TItem&gt;"/> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="activator">The activator.</param>
        /// <param name="scope">The scope.</param>
        ServiceListRegistration(IEnumerable<Service> services, ServiceListActivator activator, IScope scope)
            : base(services, activator, scope, InstanceOwnership.Container)
        {
            _activator = Enforce.ArgumentNotNull(activator, "activator");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceListRegistration&lt;TItem&gt;"/> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="scope">The scope.</param>
        public ServiceListRegistration(IEnumerable<Service> services, IScope scope)
            : this(services, new ServiceListActivator(), scope)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceListRegistration&lt;TItem&gt;"/> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="items">The items.</param>
        protected ServiceListRegistration(IEnumerable<Service> services, IScope scope, IEnumerable<Service> items)
            : this(services, scope)
        {
            Enforce.ArgumentNotNull(items, "items");

            foreach (var item in items)
                Add(item);
        }

        #region ICompositeRegistration Members

        /// <summary>
        /// Add a service (key into another component registration) to those returned
        /// in the list.
        /// </summary>
        /// <param name="item"></param>
        public void Add(Service item)
        {
            Enforce.ArgumentNotNull(item, "item");
            _activator.Items.Add(item);
        }

        #endregion

        #region IComponentRegistration Members

        /// <summary>
        /// Create a duplicate of this instance if it is semantically valid to
        /// copy it to a new context.
        /// </summary>
        /// <param name="duplicate">The duplicate.</param>
        /// <returns>True if the duplicate was created.</returns>
        public override bool DuplicateForNewContext(out IComponentRegistration duplicate)
        {
            duplicate = null;

            IScope newScope;
            if (!Scope.DuplicateForNewContext(out newScope))
                return false;

            duplicate = new ServiceListRegistration<TItem>(_activator.Items, newScope);
            return true;
        }

        #endregion
    }
}