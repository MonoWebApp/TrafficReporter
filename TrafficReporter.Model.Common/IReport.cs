﻿using System;
using System.Collections.Generic;
using TrafficReporter.Common.Enums;

namespace TrafficReporter.Model.Common
{
    public interface IReport
    {
        Guid Id { get; set; }
        Cause Cause { get; set; }
        DateTime DateCreated { get; set; }
        int Rating { get; set; }
        Direction Direction { get; set; }
        Ban Ban { get; set; }
        Guid UserId { get; set; }

    }
}