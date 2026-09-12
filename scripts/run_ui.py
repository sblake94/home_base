import os
import getpass
import subprocess
import time


def get_socket_path() -> str:
    runtime_directory = os.environ.get("XDG_RUNTIME_DIR")
    if not runtime_directory:
        runtime_directory = os.path.join("/tmp", f"homebase-{getpass.getuser()}")
    return os.path.join(runtime_directory, "homebase", "core.sock")


def wait_for_socket(socket_path: str, timeout: float = 10.0):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if os.path.exists(socket_path):
            return
        time.sleep(0.1)
    raise RuntimeError(f"HomeBase service did not create its socket: {socket_path}")

def main(debug: bool = False):
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build", "HomeBase.slnx"], check=True)

    # Run the backend required by the UI's document and chat services.
    print("Starting HomeBase Service...")
    service = subprocess.Popen([
        "dotnet",
        "run",
        "--project",
        "HomeBase.Service/HomeBase.Service.csproj",
    ])

    try:
        wait_for_socket(get_socket_path())

        # Run HomeBase UI
        print("Running HomeBase UI...")
        cmd = [
        "dotnet", 
        "run", 
        "--project", 
        "HomeBase/HomeBase.csproj",
        ]
        if debug:
            print("Debug mode enabled. Waiting for debugger to attach...")
            cmd.extend(["--", "--wait-for-debugger"])

        subprocess.run(cmd, check=True)
    except KeyboardInterrupt:
        print("\nHomeBase UI stopped by user.")
    except subprocess.CalledProcessError as e:
        print(f"\nError running HomeBase UI: {e}")
    finally:
        service.terminate()
        try:
            service.wait(timeout=5)
        except subprocess.TimeoutExpired:
            service.kill()
            service.wait()

if __name__ == "__main__":
    # Get args, look for --debug
    import sys
    if "--debug" in sys.argv:
        main(True)
    else:
        main(False)

