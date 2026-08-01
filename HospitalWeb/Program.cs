using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

var options = new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production,
    ContentRootPath = Directory.GetCurrentDirectory()
};


var builder = WebApplication.CreateBuilder(options);


//  ⁄ÿÌ· File Watcher ·„‰⁄ Œÿ√ Render inotify
builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: false
    );



// PORT «·Œ«’ »‹ Render
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);



// MVC
builder.Services.AddControllersWithViews();



// ﬁ«⁄œ… «·»Ì«‰« 

string rootPath = Directory.GetCurrentDirectory();

string appData = Path.Combine(rootPath, "App_Data");

Directory.CreateDirectory(appData);


string dbPath = Path.Combine(
    appData,
    "hospital.db"
);



// Logs

Console.WriteLine("====================================");
Console.WriteLine("Environment = " + builder.Environment.EnvironmentName);
Console.WriteLine("Content Root = " + rootPath);
Console.WriteLine("Database Path = " + dbPath);
Console.WriteLine("Database Exists = " + File.Exists(dbPath));


if (File.Exists(dbPath))
{
    Console.WriteLine(
        "Database Size = " +
        new FileInfo(dbPath).Length +
        " bytes"
    );
}


Console.WriteLine("PORT = " + port);
Console.WriteLine("====================================");



// SQLite

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    }
);



// Import
builder.Services.AddScoped<AccessImporter>();




// Build

var app = builder.Build();




// «Œ »«— ﬁ«⁄œ… «·»Ì«‰« 

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
            "DATABASE ERROR = " + ex.Message
        );
    }
}




if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}



app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();