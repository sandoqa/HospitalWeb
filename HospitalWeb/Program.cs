using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// ===============================================
// Builder
// ===============================================

var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production,
    ContentRootPath = Directory.GetCurrentDirectory()
});


// ===============================================
// Configuration
// ===============================================

builder.Configuration.Sources.Clear();

builder.Configuration.AddJsonFile(
    "appsettings.json",
    optional: false,
    reloadOnChange: false
);



// ===============================================
// Port
// ===============================================

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "5000";


builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);



// ===============================================
// MVC
// ===============================================

builder.Services.AddControllersWithViews();



// ===============================================
// Database Location
// ===============================================

string rootPath =
    Directory.GetCurrentDirectory();


string appData =
    Path.Combine(
        rootPath,
        "App_Data"
    );


Directory.CreateDirectory(appData);



string dbPath =
    Path.Combine(
        appData,
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
// Import
// ===============================================

builder.Services.AddScoped<AccessImporter>();



// ===============================================
// Build
// ===============================================

var app = builder.Build();



// ===============================================
// Test Database
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



        int doctorsCount =
            db.Doctors.Count();


        Console.WriteLine(
            "Doctors Count = " + doctorsCount
        );



        int trainingCount =
            db.TrainingRotations.Count();


        Console.WriteLine(
            "Training Count = " + trainingCount
        );



        int departmentsCount =
            db.Departments.Count();


        Console.WriteLine(
            "Departments Count = " + departmentsCount
        );

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
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();