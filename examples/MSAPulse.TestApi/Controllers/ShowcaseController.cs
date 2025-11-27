using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSAPulse.TestApi.Data;
using MSAPulse.TestApi.Models;

namespace MSAPulse.TestApi.Controllers;

[ApiController]
[Route("api/showcase")]
public class ShowcaseController : ControllerBase
{
    private readonly TestDbContext _context;
    private readonly ILogger<ShowcaseController> _logger;

    public ShowcaseController(TestDbContext context, ILogger<ShowcaseController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ========================================================================
    // SCENARIO 1: Basic Tracing & Logging
    // ========================================================================

    /// <summary>
    /// Demonstrates Correlation ID propagation across the request lifecycle.
    /// Check your logs/dashboard to see that all logs share the same 'CorrelationId'.
    /// </summary>
    [HttpGet("trace-flow")]
    public async Task<IActionResult> TraceFlow()
    {
        _logger.LogInformation("1. Request received at API endpoint.");

        await Task.Delay(50); // Simulate business logic
        _logger.LogInformation("2. Processing business logic...");

        var count = await _context.Products.CountAsync();
        _logger.LogInformation("3. Database query executed. Product count: {Count}", count);

        return Ok(new
        {
            message = "Check the logs! All 3 steps should have the same CorrelationId.",
            traceId = HttpContext.Response.Headers["X-Correlation-ID"].ToString()
        });
    }

    // ========================================================================
    // SCENARIO 2: Database Performance (Slow Query Detection)
    // ========================================================================

    /// <summary>
    /// Triggers a database query. 
    /// Since we set SlowQueryThresholdMs=0 in Program.cs, this will ALWAYS log a WARNING.
    /// Look for the yellow [WRN] log in Console or Seq.
    /// </summary>
    [HttpGet("slow-query")]
    public async Task<IActionResult> SimulateSlowQuery()
    {
        _logger.LogInformation("Executing database query (Will be flagged as SLOW)...");

        // MSAPulse Interceptor will capture this execution time
        var products = await _context.Products
            .Include(p => p.Orders) // Fetching related data
            .ToListAsync();

        return Ok(new
        {
            message = "Query executed. Check logs for 'SLOW QUERY DETECTED' warning.",
            itemCount = products.Count
        });
    }

    // ========================================================================
    // SCENARIO 3: Global Exception Handling
    // ========================================================================

    /// <summary>
    /// Simulates a 404 Not Found scenario by throwing a specific exception.
    /// MSAPulse Global Handler catches this and returns a standard JSON response (RFC 7807).
    /// </summary>
    [HttpGet("error-not-found")]
    public IActionResult SimulateNotFound()
    {
        _logger.LogInformation("Simulating resource lookup failure...");

        // This exception is automatically mapped to HTTP 404
        throw new KeyNotFoundException("The requested Product ID #999 was not found.");
    }

    /// <summary>
    /// Simulates a 400 Bad Request (Validation Error).
    /// </summary>
    [HttpGet("error-validation")]
    public IActionResult SimulateValidationError()
    {
        _logger.LogInformation("Simulating validation failure...");

        // This exception is automatically mapped to HTTP 400
        throw new ArgumentException("Invalid input: Email address is required.");
    }

    /// <summary>
    /// Simulates a Critical 500 Server Error (Database Failure).
    /// MSAPulse catches this, logs the full stack trace, but returns a safe message to the user.
    /// </summary>
    [HttpGet("error-critical")]
    public async Task<IActionResult> SimulateCriticalError()
    {
        _logger.LogInformation("Attempting to query a non-existent table...");

        try
        {
            // This will throw a SQL Exception
            await _context.Database.ExecuteSqlRawAsync("SELECT * FROM ThisTableDoesNotExist");
        }
        catch (Exception ex)
        {
            // Even if we catch it here, MSAPulse Interceptor has ALREADY logged the SQL error!
            // We re-throw it to let the Global Handler return the 500 response.
            throw new Exception("Critical Database Failure", ex);
        }

        return Ok();
    }

    // ========================================================================
    // SCENARIO 4: Parallel Processing (Thread Safety)
    // ========================================================================

    /// <summary>
    /// Demonstrates that MSAPulse captures correct Thread IDs for concurrent tasks.
    /// </summary>
    [HttpGet("parallel-tasks")]
    public async Task<IActionResult> ParallelTasks()
    {
        _logger.LogInformation("Starting parallel operations...");

        var tasks = new List<Task>();
        for (int i = 1; i <= 5; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                _logger.LogInformation("Task #{TaskId} is running on a background thread.", taskId);
            }));
        }

        await Task.WhenAll(tasks);

        _logger.LogInformation("All parallel tasks finished.");
        return Ok("Check logs for different Thread IDs.");
    }
    // ========================================================================
    // SCENARIO 5: Data Modification (POST Request & Transactions)
    // ========================================================================

    /// <summary>
    /// Simulates a data creation process.
    /// This shows 'HTTP POST' in your logs and demonstrates how INSERT commands are captured.
    /// </summary>
    [HttpPost("create-product")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        _logger.LogInformation("Starting product creation workflow...");

        if (string.IsNullOrEmpty(product.Name))
        {
            // Validation check
            throw new ArgumentNullException(nameof(product.Name), "Product name is required.");
        }

        // MSAPulse Interceptor will log the 'INSERT' SQL command
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Product '{Name}' created successfully with ID: {Id}", product.Name, product.Id);

        return CreatedAtAction(nameof(TraceFlow), new { id = product.Id }, product);
    }

    // ========================================================================
    // SCENARIO 6: The "N+1" Problem (Performance Anti-Pattern)
    // ========================================================================

    /// <summary>
    /// Deliberately triggers the infamous "N+1" problem.
    /// Instead of 1 query, this will trigger 1 query for Products + 1 query PER product for Orders.
    /// EXPECTATION: You will see a flood of SQL commands in your logs/dashboard.
    /// </summary>
    [HttpGet("n-plus-one")]
    public IActionResult TriggerNPlusOne()
    {
        _logger.LogInformation("Executing N+1 scenario (The bad way)...");

        // 1. Fetch all products (Query #1)
        var products = _context.Products.ToList();

        foreach (var product in products)
        {
            // 2. BAD PRACTICE: Fetching orders inside a loop!
            // This triggers a separate SQL query for EACH product.
            // MSAPulse will capture every single one of them.
            var orderCount = _context.Orders.Count(o => o.ProductId == product.Id);

            _logger.LogInformation("Product {Id} has {Count} orders.", product.Id, orderCount);
        }

        return Ok(new { message = "Check your logs. You should see MANY separate SQL queries instead of one." });
    }
    // ========================================================================
    // SCENARIO 7: The "N+1" Problem FIXED (Eager Loading)
    // ========================================================================

    /// <summary>
    /// Demonstrates the optimized version of the previous scenario.
    /// Instead of looping, we use '.Include()' (Eager Loading) to fetch everything in ONE go.
    /// EXPECTATION: You will see ONLY ONE optimized SQL query in your logs.
    /// </summary>
    [HttpGet("n-plus-one-fixed")]
    public async Task<IActionResult> NPlusOneFixed()
    {
        _logger.LogInformation("Executing Optimized scenario (The Good Way)...");

        // GOOD PRACTICE: Using .Include()
        // EF Core translates this into a single SQL query with a JOIN.
        // MSAPulse will log just 1 entry instead of 100.
        var products = await _context.Products
            .Include(p => p.Orders)
            .ToListAsync();

        foreach (var product in products)
        {
            // Data is already in memory, no DB call happens here!
            var orderCount = product.Orders.Count;
            _logger.LogInformation("Product {Id} has {Count} orders (Calculated in memory).", product.Id, orderCount);
        }

        return Ok(new
        {
            message = "Optimization successful! Check your logs. You should see only 1 SQL query.",
            productCount = products.Count
        });
    }
}