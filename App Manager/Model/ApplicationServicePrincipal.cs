using System;
using System.Collections.Generic;
using System.Text;

namespace App_Manager.Model
{
  class ApplicationServicePrincipal
  {
    public ServicePrincipal ServicePrincipal { get; set; }
    public Application Application { get; set; }
  }

  class ServicePrincipal
  {
    public string Id { get; set; }
    public string AppId { get; set; }
  }
}
