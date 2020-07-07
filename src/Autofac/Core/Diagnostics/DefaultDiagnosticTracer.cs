﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Autofac.Core.Resolving;
using Autofac.Core.Resolving.Pipeline;

namespace Autofac.Core.Diagnostics
{
    /// <summary>
    /// Provides a default resolve pipeline tracer that builds a multi-line string describing the end-to-end operation flow.
    /// Attach to the <see cref="OperationCompleted"/> event to receive notifications when new trace content is available.
    /// </summary>
    public class DefaultDiagnosticTracer
    {
        private const string RequestExceptionTraced = "__RequestException";

        private readonly ConcurrentDictionary<ITracingIdentifer, IndentingStringBuilder> _operationBuilders = new ConcurrentDictionary<ITracingIdentifer, IndentingStringBuilder>();

        private static readonly string[] NewLineSplit = new[] { Environment.NewLine };

        /// <summary>
        /// Event raised when a resolve operation completes, and trace data is available.
        /// </summary>
        public event EventHandler<OperationTraceCompletedArgs>? OperationCompleted;

        /// <summary>
        /// Gets the number of operations in progress being traced.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> with the number of trace IDs associated
        /// with in-progress operations being traced by this tracer.
        /// </value>
        public int OperationsInProgress => this._operationBuilders.Count;

        /// <inheritdoc/>
        public void OperationStart(ResolveOperationBase operation, ResolveRequest initiatingRequest)
        {
            var builder = _operationBuilders.GetOrAdd(operation.TracingId, k => new IndentingStringBuilder());

            builder.AppendFormattedLine(TracerMessages.ResolveOperationStarting);
            builder.AppendLine(TracerMessages.EntryBrace);
            builder.Indent();
        }

        /// <inheritdoc/>
        public void RequestStart(ResolveOperationBase operation, ResolveRequestContextBase requestContext)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                builder.AppendFormattedLine(TracerMessages.ResolveRequestStarting);
                builder.AppendLine(TracerMessages.EntryBrace);
                builder.Indent();
                builder.AppendFormattedLine(TracerMessages.ServiceDisplay, requestContext.Service);
                builder.AppendFormattedLine(TracerMessages.ComponentDisplay, requestContext.Registration.Activator.DisplayName());

                if (requestContext.DecoratorTarget is object)
                {
                    builder.AppendFormattedLine(TracerMessages.TargetDisplay, requestContext.DecoratorTarget.Activator.DisplayName());
                }

                builder.AppendLine();
                builder.AppendLine(TracerMessages.Pipeline);
            }
        }

        /// <inheritdoc/>
        public void MiddlewareEntry(ResolveOperationBase operation, ResolveRequestContextBase requestContext, IResolveMiddleware middleware)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                builder.AppendFormattedLine(TracerMessages.EnterMiddleware, middleware.ToString());
                builder.Indent();
            }
        }

        /// <inheritdoc/>
        public void MiddlewareExit(ResolveOperationBase operation, ResolveRequestContextBase requestContext, IResolveMiddleware middleware, bool succeeded)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                builder.Outdent();

                if (succeeded)
                {
                    builder.AppendFormattedLine(TracerMessages.ExitMiddlewareSuccess, middleware.ToString());
                }
                else
                {
                    builder.AppendFormattedLine(TracerMessages.ExitMiddlewareFailure, middleware.ToString());
                }
            }
        }

        /// <inheritdoc/>
        public void RequestFailure(ResolveOperationBase operation, ResolveRequestContextBase requestContext, Exception requestException)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                builder.Outdent();
                builder.AppendLine(TracerMessages.ExitBrace);

                if (requestException is DependencyResolutionException && requestException.InnerException is object)
                {
                    requestException = requestException.InnerException;
                }

                if (requestException.Data.Contains(RequestExceptionTraced))
                {
                    builder.AppendLine(TracerMessages.ResolveRequestFailedNested);
                }
                else
                {
                    builder.AppendException(TracerMessages.ResolveRequestFailed, requestException);
                }

                requestException.Data[RequestExceptionTraced] = true;
            }
        }

        /// <inheritdoc/>
        public void RequestSuccess(ResolveOperationBase operation, ResolveRequestContextBase requestContext)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                builder.Outdent();
                builder.AppendLine(TracerMessages.ExitBrace);
                builder.AppendFormattedLine(TracerMessages.ResolveRequestSucceeded, requestContext.Instance);
            }
        }

        /// <inheritdoc/>
        public void OperationFailure(ResolveOperationBase operation, Exception operationException)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                try
                {
                    builder.Outdent();
                    builder.AppendLine(TracerMessages.ExitBrace);
                    builder.AppendException(TracerMessages.OperationFailed, operationException);

                    // If we're completing the root operation, raise the event.
                    if (operation.IsTopLevelOperation)
                    {
                        OperationCompleted?.Invoke(this, new OperationTraceCompletedArgs(operation, builder.ToString()));
                    }
                }
                finally
                {
                    _operationBuilders.TryRemove(operation.TracingId, out var _);
                }
            }
        }

        /// <inheritdoc/>
        public void OperationSuccess(ResolveOperationBase operation, object resolvedInstance)
        {
            if (_operationBuilders.TryGetValue(operation.TracingId, out var builder))
            {
                try
                {
                    builder.Outdent();
                    builder.AppendLine(TracerMessages.ExitBrace);
                    builder.AppendFormattedLine(TracerMessages.OperationSucceeded, resolvedInstance);

                    // If we're completing the root operation, raise the event.
                    if (operation.IsTopLevelOperation)
                    {
                        OperationCompleted?.Invoke(this, new OperationTraceCompletedArgs(operation, builder.ToString()));
                    }
                }
                finally
                {
                    _operationBuilders.TryRemove(operation.TracingId, out var _);
                }
            }
        }

        /// <summary>
        /// Provides a string builder that auto-indents lines.
        /// </summary>
        private class IndentingStringBuilder
        {
            private const int IndentSize = 2;

            private readonly StringBuilder _builder;
            private int _indentCount;

            public IndentingStringBuilder()
            {
                _builder = new StringBuilder();
            }

            public void Indent()
            {
                _indentCount++;
            }

            public void Outdent()
            {
                if (_indentCount == 0)
                {
                    throw new InvalidOperationException(TracerMessages.OutdentFailure);
                }

                _indentCount--;
            }

            public void AppendFormattedLine(string format, params object?[] args)
            {
                AppendIndent();
                _builder.AppendFormat(CultureInfo.CurrentCulture, format, args);
                _builder.AppendLine();
            }

            public void AppendException(string message, Exception ex)
            {
                AppendIndent();
                _builder.AppendLine(message);

                var exceptionBody = ex.ToString().Split(NewLineSplit, StringSplitOptions.None);

                Indent();

                foreach (var exceptionLine in exceptionBody)
                {
                    AppendLine(exceptionLine);
                }

                Outdent();
            }

            public void AppendLine()
            {
                // No indent on a blank line.
                _builder.AppendLine();
            }

            public void AppendLine(string value)
            {
                AppendIndent();
                _builder.AppendLine(value);
            }

            private void AppendIndent()
            {
                _builder.Append(' ', IndentSize * _indentCount);
            }

            public override string ToString()
            {
                return _builder.ToString();
            }
        }
    }
}
