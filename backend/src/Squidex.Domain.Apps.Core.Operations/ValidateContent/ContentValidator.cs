﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Domain.Apps.Core.ValidateContent.Validators;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Json.Objects;
using Squidex.Infrastructure.Validation;

namespace Squidex.Domain.Apps.Core.ValidateContent
{
    public sealed class ContentValidator
    {
        private readonly PartitionResolver partitionResolver;
        private readonly ValidationContext context;
        private readonly IEnumerable<IValidatorsFactory> factories;
        private readonly ILogger<ContentValidator> log;
        private readonly ConcurrentBag<ValidationError> errors = new ConcurrentBag<ValidationError>();

        public IReadOnlyCollection<ValidationError> Errors
        {
            get => errors;
        }

        public ContentValidator(PartitionResolver partitionResolver, ValidationContext context, IEnumerable<IValidatorsFactory> factories,
            ILogger<ContentValidator> log)
        {
            Guard.NotNull(context);
            Guard.NotNull(factories);
            Guard.NotNull(partitionResolver);
            Guard.NotNull(log);

            this.context = context;
            this.factories = factories;
            this.partitionResolver = partitionResolver;

            this.log = log;
        }

        private void AddError(IEnumerable<string> path, string message)
        {
            var pathString = path.ToPathString();

            errors.Add(new ValidationError(message, pathString));
        }

        public ValueTask ValidateInputPartialAsync(ContentData data)
        {
            Guard.NotNull(data);

            var validator = CreateSchemaValidator(true);

            return validator.ValidateAsync(data, context, AddError);
        }

        public ValueTask ValidateInputAsync(ContentData data)
        {
            Guard.NotNull(data);

            var validator = CreateSchemaValidator(false);

            return validator.ValidateAsync(data, context, AddError);
        }

        public ValueTask ValidateContentAsync(ContentData data)
        {
            Guard.NotNull(data);

            var validator = new AggregateValidator(CreateContentValidators(), log);

            return validator.ValidateAsync(data, context, AddError);
        }

        private IValidator CreateSchemaValidator(bool isPartial)
        {
            var fieldValidators = new Dictionary<string, (bool IsOptional, IValidator Validator)>(context.Schema.Fields.Count);

            foreach (var field in context.Schema.Fields)
            {
                fieldValidators[field.Name] = (!field.RawProperties.IsRequired, CreateFieldValidator(field, isPartial));
            }

            return new ObjectValidator<ContentFieldData>(fieldValidators, isPartial, "field");
        }

        private IValidator CreateFieldValidator(IRootField field, bool isPartial)
        {
            var valueValidator = CreateValueValidator(field);

            var partitioning = partitionResolver(field.Partitioning);
            var partitioningValidators = new Dictionary<string, (bool IsOptional, IValidator Validator)>();

            foreach (var partitionKey in partitioning.AllKeys)
            {
                var optional = partitioning.IsOptional(partitionKey);

                partitioningValidators[partitionKey] = (optional, valueValidator);
            }

            var typeName = partitioning.ToString()!;

            return new AggregateValidator(
                CreateFieldValidators(field)
                    .Union(Enumerable.Repeat(
                        new ObjectValidator<IJsonValue>(partitioningValidators, isPartial, typeName), 1)), log);
        }

        private IValidator CreateValueValidator(IField field)
        {
            return new FieldValidator(new AggregateValidator(CreateValueValidators(field), log), field);
        }

        private IEnumerable<IValidator> CreateContentValidators()
        {
            return factories.SelectMany(x => x.CreateContentValidators(context, CreateValueValidator));
        }

        private IEnumerable<IValidator> CreateValueValidators(IField field)
        {
            return factories.SelectMany(x => x.CreateValueValidators(context, field, CreateValueValidator));
        }

        private IEnumerable<IValidator> CreateFieldValidators(IField field)
        {
            return factories.SelectMany(x => x.CreateFieldValidators(context, field, CreateValueValidator));
        }
    }
}
