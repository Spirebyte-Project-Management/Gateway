﻿using System;
using System.Threading;
using Convey.HTTP;

namespace Spirebyte.APIGateway.Correlation;

internal sealed class CorrelationIdFactory : ICorrelationIdFactory
{
    private static readonly AsyncLocal<CorrelationIdHolder> Holder = new();

    private static string CorrelationId
    {
        get => Holder.Value?.Id;
        set
        {
            var holder = Holder.Value;
            if (holder is { }) holder.Id = null;

            if (value is { }) Holder.Value = new CorrelationIdHolder { Id = value };
        }
    }

    public string Create()
    {
        if (!string.IsNullOrWhiteSpace(CorrelationId)) return CorrelationId;

        CorrelationId = Guid.NewGuid().ToString("N");
        return CorrelationId;
    }

    private class CorrelationIdHolder
    {
        public string Id;
    }
}