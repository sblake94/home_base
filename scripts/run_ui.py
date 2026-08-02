import subprocess

def main():
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build"], check=True)

    # Run HomeBase UI
    print("Running HomeBase UI...")
    subprocess.run(["dotnet", "run", "--project", "HomeBase"], check=True)

if __name__ == "__main__":
    main()

