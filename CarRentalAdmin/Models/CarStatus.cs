﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace CarRentalAdmin.Models;

public partial class CarStatus
{
    public int CarStatusId { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();
}