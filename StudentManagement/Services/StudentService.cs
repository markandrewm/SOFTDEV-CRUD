using Microsoft.Extensions.Logging;
using StudentManagement.Interfaces;
using StudentManagement.Models;
using StudentManagement.ViewModels;

namespace StudentManagement.Services
{
    /// <summary>
    /// Implements the application/business logic for Students, sitting between the Controllers
    /// and the Repository. Responsible for: mapping between Entities and DTOs/ViewModels,
    /// enforcing business rules (uniqueness checks), and logging.
    /// </summary>
    public class StudentService : IStudentService
    {
        private readonly IStudentRepository _repository;
        private readonly ILogger<StudentService> _logger;

        public StudentService(IStudentRepository repository, ILogger<StudentService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<StudentIndexViewModel> GetStudentListAsync(
            string? searchTerm, string sortColumn, string sortDirection, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var result = await _repository.GetPagedAsync(searchTerm, sortColumn, sortDirection, pageNumber, pageSize);

            return new StudentIndexViewModel
            {
                Students = result.Items,
                TotalRecords = result.TotalCount,
                SearchTerm = searchTerm,
                SortColumn = sortColumn,
                SortDirection = sortDirection,
                CurrentPage = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<StudentDto>> GetAllAsync()
        {
            var students = await _repository.GetAllAsync();
            return students.Select(MapToDto);
        }

        public async Task<StudentDto?> GetByIdAsync(Guid id)
        {
            var student = await _repository.GetByIdAsync(id);
            return student is null ? null : MapToDto(student);
        }

        public async Task<(bool Success, StudentDto? Student, string? ErrorMessage)> CreateAsync(StudentCreateUpdateDto dto)
        {
            if (await _repository.StudentNumberExistsAsync(dto.StudentNumber))
            {
                return (false, null, $"Student number '{dto.StudentNumber}' is already in use.");
            }

            if (await _repository.EmailExistsAsync(dto.Email))
            {
                return (false, null, $"Email '{dto.Email}' is already in use.");
            }

            var entity = new Student
            {
                StudentNumber = dto.StudentNumber.Trim(),
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = dto.Email.Trim(),
                Course = dto.Course.Trim(),
                YearLevel = dto.YearLevel
            };

            var created = await _repository.AddAsync(entity);
            _logger.LogInformation("Created student {StudentNumber} (Id={StudentId})", created.StudentNumber, created.StudentId);

            return (true, MapToDto(created), null);
        }

        public async Task<(bool Success, StudentDto? Student, string? ErrorMessage)> UpdateAsync(Guid id, StudentCreateUpdateDto dto)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
            {
                return (false, null, "Student not found.");
            }

            if (await _repository.StudentNumberExistsAsync(dto.StudentNumber, excludeStudentId: id))
            {
                return (false, null, $"Student number '{dto.StudentNumber}' is already in use by another student.");
            }

            if (await _repository.EmailExistsAsync(dto.Email, excludeStudentId: id))
            {
                return (false, null, $"Email '{dto.Email}' is already in use by another student.");
            }

            existing.StudentNumber = dto.StudentNumber.Trim();
            existing.FirstName = dto.FirstName.Trim();
            existing.LastName = dto.LastName.Trim();
            existing.Email = dto.Email.Trim();
            existing.Course = dto.Course.Trim();
            existing.YearLevel = dto.YearLevel;

            await _repository.UpdateAsync(existing);
            _logger.LogInformation("Updated student Id={StudentId}", id);

            return (true, MapToDto(existing), null);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var deleted = await _repository.DeleteAsync(id);
            if (deleted)
            {
                _logger.LogInformation("Soft-deleted student Id={StudentId}", id);
            }
            return deleted;
        }

        /// <summary>Centralized Entity -> DTO mapping. In a larger app this would use AutoMapper.</summary>
        private static StudentDto MapToDto(Student student) => new()
        {
            StudentId = student.StudentId,
            StudentNumber = student.StudentNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            Course = student.Course,
            YearLevel = student.YearLevel,
            DateCreated = student.DateCreated,
            DateUpdated = student.DateUpdated
        };
    }
}
