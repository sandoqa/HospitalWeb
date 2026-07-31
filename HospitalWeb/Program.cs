using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;

var builder = WebApplication.CreateBuilder(args);


// ==================================================
// MVC
// ==================================================

builder.Services.AddControllersWithViews();



// ==================================================
//  ÕœÌœ ﬁ«⁄œ… «·»Ì«‰«  („”«— À«» )
// ==================================================

string dbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data",
    "hospital.db"
);


// ≈‰‘«¡ „Ã·œ App_Data ≈–« €Ì— „ÊÃÊœ

string? dbFolder = Path.GetDirectoryName(dbPath);

if (!string.IsNullOrEmpty(dbFolder))
{
    Directory.CreateDirectory(dbFolder);
}



// ==================================================
// „⁄·Ê„«  ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

Console.WriteLine("====================================");

Console.WriteLine(
    "Environment  = " +
    builder.Environment.EnvironmentName
);


Console.WriteLine(
    "Content Root = " +
    Directory.GetCurrentDirectory()
);


Console.WriteLine(
    "DB Path      = " +
    dbPath
);


Console.WriteLine(
    "DB Exists    = " +
    File.Exists(dbPath)
);



if (File.Exists(dbPath))
{
    FileInfo info = new FileInfo(dbPath);

    Console.WriteLine(
        "DB Size      = " +
        info.Length +
        " bytes"
    );
}


Console.WriteLine("====================================");




// ==================================================
// SQLite
// ==================================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    });




// ==================================================
// Access Importer
// ==================================================

builder.Services.AddScoped<AccessImporter>();





var app = builder.Build();





// ==================================================
// «Œ »«— «·« ’«· »ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

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
            "Database Connected = " +
            connected
        );


        if (connected)
        {
            Console.WriteLine(
                "Doctors Count = " +
                db.Doctors.Count()
            );


            Console.WriteLine(
                "Training Count = " +
                db.TrainingRotations.Count()
            );
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine("DATABASE ERROR");

        Console.WriteLine(
            ex.ToString()
        );
    }
}





// ==================================================
// Error Handling
// ==================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}




// ==================================================
// HTTPS
// ==================================================

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}




// ==================================================
// Middleware
// ==================================================

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();





// ==================================================
// Default Route
// ==================================================

app.MapControllerRoute(
    name: "default",
    pattern:
    "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();