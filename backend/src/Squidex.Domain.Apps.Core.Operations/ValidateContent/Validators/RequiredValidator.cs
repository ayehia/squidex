﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Infrastructure.Translations;

namespace Squidex.Domain.Apps.Core.ValidateContent.Validators
{
    public class RequiredValidator : IValidator
    {
        public ValueTask ValidateAsync(object? value, ValidationContext context, AddError addError)
        {
            if (value.IsNullOrUndefined() && !context.IsOptional)
            {
                addError(context.Path, T.Get("contents.validation.required"));
            }

            return default;
        }
    }
}
