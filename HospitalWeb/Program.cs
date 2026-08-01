using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// ===============================================
// Render Production Builder
// ===============================================

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions
    {
        Args = args,
        EnvironmentName = Environments.Production,
        ContentRootPath = Directory.GetCurrentDirectory()
    });


// ===============================================
// Disable File Watching
// ===============================================

builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: false
    )
    .AddEnvironmentVariables();



// ===============================================
// Render Port
// ===============================================

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "10000";


builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);



// ===============================================
// MVC
// ===============================================

builder.Services.AddControllersWithViews();



// ===============================================
// Database Path
// ===============================================

var appDataPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data"
);


Directory.CreateDirectory(appDataPath);


var dbPath = Path.Combine(
    appDataPath,
    "hospital.db"
);



// ===============================================
// Database Logs
// ===============================================

Console.WriteLine("====================================");

Console.WriteLine(
    "Environment = " +
    builder.Environment.EnvironmentName
);


Console.WriteLine(
    "Content Root = " +
    Directory.GetCurrentDirectory()
);


Console.WriteLine(
    "DB Path = " +
    dbPath
);


Console.WriteLine(
    "DB Exists = " +
    File.Exists(dbPath)
);


if (File.Exists(dbPath))
{
    Console.WriteLine(
        "DB Size = " +
        new FileInfo(dbPath).Length +
        " bytes"
    );
}


Console.WriteLine(
    "PORT = " + port
);


Console.WriteLine("====================================");




// ===============================================
// SQLite
// ===============================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    });




// ===============================================
// Access Importer
// ===============================================

builder.Services.AddScoped<AccessImporter>();




// ===============================================
// Build
// ===============================================

var app = builder.Build();




// ===============================================
// Database Check
// ===============================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db =
            scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();


        bool connected =
            db.Database.CanConnect();


        Console.WriteLine(
            "Database Connected = " + connected
        );


        if (connected)
        {
            int doctors =
                db.Doctors.Count();


            int training =
                db.TrainingRotations.Count();


            Console.WriteLine(
                "Doctors Count = " + doctors
            );


            Console.WriteLine(
                "Training Count = " + training
            );
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR = " + ex.Message
        );
    }
}




// ===============================================
// Middleware
// ===============================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();




// ===============================================
// Default Route
// ===============================================

app.MapControllerRoute(
    name: "default",
    pattern:
    "{controller=Doctors}/{action=Index}/{id?}"
);




// ===============================================
// Run
// ===============================================

app.Run();