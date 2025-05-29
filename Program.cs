using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

// Классы сотрудников (из 3-й лабораторной)
public abstract class Employee
{
    public string Name { get; set; }
    public DateTime EmploymentDate { get; set; }
    public decimal Rate { get; set; }
    public string EmployeeType { get; protected set; }

    public Employee(string name, DateTime employmentDate, decimal rate)
    {
        Name = name;
        EmploymentDate = employmentDate;
        Rate = rate;
    }

    public abstract void GetInfo();
    public abstract decimal GetPrice();
}

public class KitchenWorker : Employee
{
    public int HoursWorked { get; set; }

    public KitchenWorker(string name, DateTime employmentDate, decimal rate, int hoursWorked) 
        : base(name, employmentDate, rate)
    {
        HoursWorked = hoursWorked;
        EmployeeType = "KitchenWorker";
    }

    public override void GetInfo()
    {
        Console.WriteLine($"Работник кухни: {Name}, Дата трудоустройства: {EmploymentDate.ToShortDateString()}, Ставка: {Rate}, Отработано часов: {HoursWorked}");
    }

    public override decimal GetPrice()
    {
        return Rate * HoursWorked;
    }
}

public class Waiter : Employee
{
    public int HoursWorked { get; set; }
    public decimal Tips { get; set; }

    public Waiter(string name, DateTime employmentDate, decimal rate, int hoursWorked, decimal tips) 
        : base(name, employmentDate, rate)
    {
        HoursWorked = hoursWorked;
        Tips = tips;
        EmployeeType = "Waiter";
    }

    public override void GetInfo()
    {
        Console.WriteLine($"Официант: {Name}, Дата трудоустройства: {EmploymentDate.ToShortDateString()}, Ставка: {Rate}, Отработано часов: {HoursWorked}, Чаевые: {Tips}");
    }

    public override decimal GetPrice()
    {
        return Rate * HoursWorked + Tips;
    }
}

public class Manager : Employee
{
    public decimal Bonus { get; set; }

    public Manager(string name, DateTime employmentDate, decimal rate, decimal bonus) 
        : base(name, employmentDate, rate)
    {
        Bonus = bonus;
        EmployeeType = "Manager";
    }

    public override void GetInfo()
    {
        Console.WriteLine($"Менеджер: {Name}, Дата трудоустройства: {EmploymentDate.ToShortDateString()}, Ставка: {Rate}, Премия: {Bonus}");
    }

    public override decimal GetPrice()
    {
        int yearsWorked = DateTime.Now.Year - EmploymentDate.Year;
        return Rate + Bonus * yearsWorked;
    }
}

public class JuniorManager : Manager
{
    public JuniorManager(string name, DateTime employmentDate, decimal rate, decimal bonus) 
        : base(name, employmentDate, rate, bonus)
    {
        EmployeeType = "JuniorManager";
    }

    public override decimal GetPrice()
    {
        int yearsWorked = DateTime.Now.Year - EmploymentDate.Year;
        decimal baseSalary = Rate;

        if (DateTime.Now.Month == 6 || DateTime.Now.Month == 12)
        {
            baseSalary += Bonus * yearsWorked;
        }
        return baseSalary;
    }

    public override void GetInfo()
    {
        Console.WriteLine($"Младший менеджер: {Name}, Дата трудоустройства: {EmploymentDate.ToShortDateString()}, Ставка: {Rate}, Премия: {Bonus}");
    }
}

// Класс для работы с БД
public class RestaurantDatabase
{
    private readonly string _connectionString;

    public RestaurantDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void InitializeDatabase()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            
            var command = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Employees')
                BEGIN
                    CREATE TABLE Employees (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        EmploymentDate DATE NOT NULL,
                        Rate DECIMAL(18,2) NOT NULL,
                        EmployeeType NVARCHAR(50) NOT NULL,
                        HoursWorked INT NULL,
                        Tips DECIMAL(18,2) NULL,
                        Bonus DECIMAL(18,2) NULL
                    )
                END", connection);
            
            command.ExecuteNonQuery();
        }
    }

    public void SaveEmployee(Employee employee)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            
            var command = new SqlCommand(@"
                INSERT INTO Employees (Name, EmploymentDate, Rate, EmployeeType, HoursWorked, Tips, Bonus)
                VALUES (@Name, @EmploymentDate, @Rate, @EmployeeType, @HoursWorked, @Tips, @Bonus)", connection);
            
            command.Parameters.AddWithValue("@Name", employee.Name);
            command.Parameters.AddWithValue("@EmploymentDate", employee.EmploymentDate);
            command.Parameters.AddWithValue("@Rate", employee.Rate);
            command.Parameters.AddWithValue("@EmployeeType", employee.EmployeeType);

            // Добавляем параметры в зависимости от типа сотрудника
            if (employee is KitchenWorker kitchenWorker)
            {
                command.Parameters.AddWithValue("@HoursWorked", kitchenWorker.HoursWorked);
                command.Parameters.AddWithValue("@Tips", DBNull.Value);
                command.Parameters.AddWithValue("@Bonus", DBNull.Value);
            }
            else if (employee is Waiter waiter)
            {
                command.Parameters.AddWithValue("@HoursWorked", waiter.HoursWorked);
                command.Parameters.AddWithValue("@Tips", waiter.Tips);
                command.Parameters.AddWithValue("@Bonus", DBNull.Value);
            }
            else if (employee is Manager manager)
            {
                command.Parameters.AddWithValue("@HoursWorked", DBNull.Value);
                command.Parameters.AddWithValue("@Tips", DBNull.Value);
                command.Parameters.AddWithValue("@Bonus", manager.Bonus);
            }
            
            command.ExecuteNonQuery();
        }
    }

    // Выгрузка из БД
    public List<Employee> LoadEmployees()
    {
        var employees = new List<Employee>();
        
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            
            var command = new SqlCommand("SELECT * FROM Employees", connection);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader["Name"].ToString();
                    var employmentDate = (DateTime)reader["EmploymentDate"];
                    var rate = (decimal)reader["Rate"];
                    var employeeType = reader["EmployeeType"].ToString();

                    Employee employee = employeeType switch
                    {
                        "KitchenWorker" => new KitchenWorker(
                            name,
                            employmentDate,
                            rate,
                            (int)reader["HoursWorked"]),
                        
                        "Waiter" => new Waiter(
                            name,
                            employmentDate,
                            rate,
                            (int)reader["HoursWorked"],
                            (decimal)reader["Tips"]),
                            
                        "Manager" => new Manager(
                            name,
                            employmentDate,
                            rate,
                            (decimal)reader["Bonus"]),
                            
                        "JuniorManager" => new JuniorManager(
                            name,
                            employmentDate,
                            rate,
                            (decimal)reader["Bonus"]),
                            
                        _ => throw new InvalidOperationException("Unknown employee type")
                    };
                    
                    employees.Add(employee);
                }
            }
        }
        
        return employees;
    }
}

class Program
{
    static void Main()
    {
        // Строка подключения к локальной базе данных SQL Server
        string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";
        
        var database = new RestaurantDatabase(connectionString);
        database.InitializeDatabase();

        // Создаем тестовых сотрудников
        var employees = new List<Employee>
        {
            new KitchenWorker("Повар1", new DateTime(2023, 1, 15), 100, 160),
            new KitchenWorker("Повар2", new DateTime(2023, 2, 20), 110, 170),
            new Waiter("Официант1", new DateTime(2023, 3, 10), 80, 150, 5000),
            new Waiter("Официант2", new DateTime(2022, 4, 5), 90, 160, 6000),
            new Manager("Менеджер", new DateTime(2020, 5, 2), 20000, 10000),
            new JuniorManager("Младший менеджер", new DateTime(2021, 6, 8), 18000, 8000)
        };

        // Сохраняем сотрудников в базу данных
        foreach (var employee in employees)
        {
            database.SaveEmployee(employee);
        }

        //Загружаем сотрудников из базы данных
        var loadedEmployees = database.LoadEmployees();

        Console.WriteLine("Информация о сотрудниках:");
        Console.WriteLine("-------------------------");
        
        foreach (var employee in loadedEmployees)
        {
            employee.GetInfo();
            Console.WriteLine($"Зарплата: {employee.GetPrice():C}");
            Console.WriteLine();
        }
    }
}