using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace FlintecChatBotApp.Components.Models
{
    internal class Database
    {
    await using var conn = new SqliteConnection("Data Source=FlintecChatBot.db");
    await conn.OpenAsync();
    await var command = new SqlCommand("", conn);
    await using var datareader = await cmd.ExecuteReaderAsync();

    while (await datareader.ReadAsync())
    {
        var id = datareader.GetInt32(0);
        var name = datareader.GetString(1);
        var age = datareader.GetInt32(2);
        var email = datareader.GetString(3);
        Console.WriteLine($"ID: {id}, Name: {name}, Age: {age}, Email: {email}");
    } 





}


