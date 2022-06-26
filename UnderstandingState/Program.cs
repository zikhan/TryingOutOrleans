using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
using System.Linq;

using IHost? host = new HostBuilder()
    .UseOrleans(builder =>
    {
        builder.AddMemoryGrainStorageAsDefault();
        builder.UseLocalhostClustering();
    })
    .Build();

await host.StartAsync();

var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

// Create a grain for user 1
IStateHolderGrain? user1 = grainFactory.GetGrain<IStateHolderGrain>("user1");

// Get current list for user1
WriteTodos((await user1.GetTodoItems()));

// Add A value
await user1.AddTodo("First action");

WriteTodos((await user1.GetTodoItems()));

// Create a grain for user 2 and test concurrency
IStateHolderGrain? user2 = grainFactory.GetGrain<IStateHolderGrain>("user2");

// Add A value for user 2
var t1user2 = Task.Run(async () => {
    for (int i = 0; i < 100; i++)
        await user2.AddTodo($"action {i}");
});

// Remove the added value user 2
var t2user2 = Task.Run(async () => {
    TodoItem? next = await user2.GetNextItem();
    while (next is not null)
    {
        await user2.RemoveTodo(next);
        next = await user2.GetNextItem();
    }
});

// Add A value for user 1
var t1user1 = Task.Run(async () => {
    for (int i = 0; i < 100; i++)
        await user1.AddTodo($"action {i}");
});

// Remove the added value user 1
var t2user1 = Task.Run(async () => {
    TodoItem? next = await user1.GetNextItem();
    while (next is not null)
    {
        await user1.RemoveTodo(next);
        next = await user1.GetNextItem();
    }
});

Task.WaitAll(t1user2, t2user2, t1user1, t2user1);

Console.WriteLine("user1");
WriteTodos((await user1.GetTodoItems()));

Console.WriteLine("user2");
WriteTodos((await user2.GetTodoItems()));

await host.StopAsync();

static void WriteTodos(List<TodoItem> todos)
{
    Console.WriteLine("List:");
    Console.WriteLine(todos.Aggregate("", (acc, item) => acc + item + Environment.NewLine));
}

public record TodoItem(string Action);

public interface IStateHolderGrain : IGrainWithStringKey
{
    Task<List<TodoItem>> GetTodoItems();
    Task<TodoItem?> GetNextItem();
    Task<List<TodoItem>> AddTodo(string action);
    Task<List<TodoItem>> RemoveTodo(TodoItem item);
}

public class StateGrain : Grain, IStateHolderGrain
{
    private readonly IPersistentState<List<TodoItem>> _todoList;

    public StateGrain([PersistentState("todoList")]IPersistentState<List<TodoItem>> todoList)
    {
        _todoList = todoList;
    }
    public Task<List<TodoItem>> GetTodoItems() => Task.FromResult(_todoList.State);

    public Task<TodoItem?> GetNextItem() => Task.FromResult(_todoList.State?.FirstOrDefault());
    public async Task<List<TodoItem>> AddTodo(string action)
    {
        if (!_todoList.RecordExists)
        {
            _todoList.State = new List<TodoItem>();
        }

        var newItem = new TodoItem(action);
        if (_todoList.State.Exists(x => x == newItem))
            return _todoList.State;

        _todoList.State.Add(newItem);
        await _todoList.WriteStateAsync();
        Console.WriteLine("{1}: Added Todo {0}", newItem, this.GetPrimaryKeyString());
        return _todoList.State;
    }

    public async Task<List<TodoItem>> RemoveTodo(TodoItem item)
    {
        if (!_todoList.RecordExists)
        {
            return _todoList.State;
        }

        _todoList.State.Remove(item);
        await _todoList.WriteStateAsync();
        Console.WriteLine("{1}: Removed Todo {0}", item, this.GetPrimaryKeyString());
        return _todoList.State;
    }
}
