using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Moderation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Repository.DBContext;
using Repository.Entities;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class MoodMusicServiceTests
    {
        // Skipping service test for MoodMusic as it uses DbContext directly and requires complex setup.
        // Will focus on Controller test.
    }
}
