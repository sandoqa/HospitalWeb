using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// =====================================
// Render inotify Fix
// =====================================

Environment.SetEnvironmentVariable(
    "DOTNET_USE_POLLING_FILE_WATCHER",
    "true"
);



var options = new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production,
    ContentRootPath = Directory.GetCurrentDirectory()
};



var builder = WebApplication.CreateBuilder(options);



// =====================================
// Disable Configuration File Watching
// =====================================

builder.Configuration.Sources.Clear();

builder.Configuration.AddJsonFile(
    "appsettings.json",
    optional: false,
    reloadOnChange: false
);



// =====================================
// Render PORT
// =====================================

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";


builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);



// =====================================
// MVC
// =====================================

builder.Services.AddControllersWithViews();



// =====================================
// SQLite Database
// =====================================

string rootPath = Directory.GetCurrentDirectory();


string appDataPath = Path.Combine(
    rootPath,
    "App_Data"
);



if (!Directory.Exists(appDataPath))
{
    Directory.CreateDirectory(appDataPath);
}



string dbPath = Path.Combine(
    appDataPath,
    "hospital.db"
);



// =====================================
// Logs
// =====================================

Console.WriteLine("====================================");

Console.WriteLine(
    "Environment = " +
    builder.Environment.EnvironmentName
);


Console.WriteLine(
    "Content Root = " +
    rootPath
);


Console.WriteLine(
    "Database Path = " +
    dbPath
);


Console.WriteLine(
    "Database Exists = " +
    File.Exists(dbPath)
);



if (File.Exists(dbPath))
{
    Console.WriteLine(
        "Database Size = " +
        new FileInfo(dbPath).Length +
        " bytes"
    );
}



Console.WriteLine(
    "PORT = " +
    port
);


Console.WriteLine("====================================");



// =====================================
// Entity Framework SQLite
// =====================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    }
);



// =====================================
// Access Importer
// =====================================

builder.Services.AddScoped<AccessImporter>();



// =====================================
// Build
// =====================================

var app = builder.Build();



// =====================================
// Database Test
// =====================================

using (var scope = app.Services.CreateScope())
{
    try
    {

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();



        Console.WriteLine(
            "Database Connected = " +
            db.Database.CanConnect()
        );



        Console.WriteLine(
            "Doctors Count = " +
            db.Doctors.Count()
        );



        Console.WriteLine(
            "Training Count = " +
            db.TrainingRotations.Count()
        );



        Console.WriteLine(
            "Departments Count = " +
            db.Departments.Count()
        );

    }
    catch (Exception ex)
    {

        Console.WriteLine(
            "DATABASE ERROR = " +
            ex
        );

    }
}



// =====================================
// Production Error Handling
// =====================================

if (!app.Environment.IsDevelopment())
{

    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();

}



// =====================================
// Middleware
// =====================================

app.UseStaticFiles();


app.UseRouting();


app.UseAuthorization();



// =====================================
// Default Route
// =====================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();