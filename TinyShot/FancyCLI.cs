using Spectre.Console;

namespace TinyShot
{
    internal class FancyCLI
    {
        public static void ShowMenu()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            AnsiConsole.Clear();


            AnsiConsole.Write(
                new FigletText("TinyShot")
                    .Centered()
                    .Color(Color.Orange1));


            AnsiConsole.Write(new Panel("[bold yellow]Welcome to TinyShot — your friendly screen capture tool![/]")
                .Border(BoxBorder.Double)
                .BorderStyle(new Style(Color.Green))
                .Padding(1, 1, 1, 1)
                .Header("[bold blue]Main Menu[/]", Justify.Center));


            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold green]Choose your destiny:[/]")
                        .PageSize(6)
                        .AddChoices([
                            "🚀 Start capture",
                            "📄 Display logs",
                            "⚙️ Settings",
                            "❌ Quit"
                        ]));

                switch (choice)
                {
                    case "🚀 Start capture":
                        AnsiConsole.MarkupLine("[green]Starting screen capture...[/]");
                        StartCapture();
                        break;

                    case "📄 Display logs":
                        AnsiConsole.MarkupLine("[yellow]Showing logs (soon™)...[/]");
                        break;

                    case "⚙️ Settings":
                        AnsiConsole.MarkupLine("[blue]Settings not available yet.[/]");
                        break;

                    case "❌ Quit":
                        AnsiConsole.Write(new Panel("[bold white]Goodbye ! ^_^...[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderStyle(new Style(Color.Red))
                            .Padding(1, 1));
                        Thread.Sleep(1500);
                        // Environment.Exit(0);
                        break;

                    default:
                        break;
                }

                AnsiConsole.MarkupLine("[grey]Press any key to return to the menu...[/]");
                Console.ReadKey(true);
                AnsiConsole.Clear();
            }
        }

        private static void StartCapture()
        {

            AnsiConsole.MarkupLine("[italic]Capture logic to be implemented...[/]");
        }
    }
}
