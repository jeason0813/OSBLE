﻿using System;
using OSBLE.Models.Assignments;

namespace OSBLE.Interfaces
{
    public interface IAssignment
    {
        int Id { get; set; }
        AssignmentTypes AssignmentType { get; set; }
        int CourseId { get; set; }
        ICourse Course { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        DateTime ReleaseDate { get; set; }
        DateTime DueDate { get; set; }
    }
}
