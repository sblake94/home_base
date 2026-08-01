using System.Threading.Tasks;
using HomeBase.Models;

namespace HomeBase.Services;

public interface IChatService
{
    public Task<ChatMessage> SendMessage(string message);
}
