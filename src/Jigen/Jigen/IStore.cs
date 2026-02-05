using Jigen.DataStructures;

namespace Jigen;

public interface IStore
{
  Task SaveAsync(CancellationToken cancellationToken);

  Task Close();
}