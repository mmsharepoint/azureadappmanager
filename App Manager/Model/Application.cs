using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Graph;

namespace App_Manager.Model
{
  class Application
  {
    public string Id { get; set; }
    public string AppId { get; set; }
    public IEnumerable<AppRole> AppRoles { get; set; }
  }

  class ApplicationCollection
  {
    public Application[] value { get; set; }
  }
}
