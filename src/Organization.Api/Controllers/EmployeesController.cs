using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Api.Model;

namespace Organization.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private static readonly Dictionary<Guid, Employee> Employees;

        static EmployeesController()
        {
            var employees = new[]
            {
                new Employee { Id = new Guid("86092B43-FFC5-4947-A850-AE890649606D"), FirstName = "John", LastName = "Doe" },
                new Employee { Id = new Guid("0C742DB0-EF36-416A-8364-69C4142DAD12"), FirstName = "Emily", LastName = "Smith" }
            };
            Employees = new Dictionary<Guid, Employee>(employees.ToDictionary(e => e.Id));
        }

        [HttpGet]
        public ActionResult<IEnumerable<Employee>> Get()
        {
            return Employees.Values.ToList();
        }

        [HttpGet("{id}")]
        public ActionResult<Employee> GetById(Guid id)
        {
            if (!Employees.TryGetValue(id, out var result))
            {
                return NotFound();
            }

            return result;
        }

        [HttpPost]
        public ActionResult Post([FromBody] Employee item)
        {
            if (item.Id == Guid.Empty)
            {
                item.Id = Guid.NewGuid();
            }

            if (!Employees.TryAdd(item.Id, item))
            {
                return BadRequest(new { Error = $"Object with Id {item.Id} already exists." });
            }

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public ActionResult Put(Guid id, [FromBody] Employee item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            if (!Employees.TryGetValue(id, out _))
            {
                return NotFound();
            }

            Employees[id] = item;
            return NoContent();
        }

        [HttpDelete("{id}")]
        public ActionResult Delete(Guid id)
        {
            if (!Employees.Remove(id))
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
