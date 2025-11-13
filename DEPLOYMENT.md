# SentryXDR - Microsoft XDR Automated Remediation Platform

Multi-tenant Azure Function App for Microsoft XDR automated remediation actions.

## Quick Deploy

```powershell
.\Deployment\deploy.ps1 -ResourceGroupName "SentryXDR-RG" -Location "eastus" -MultiTenantClientId "your-id" -MultiTenantClientSecret "your-secret"
```

## API Example

```json
POST /api/xdr/remediate
{
  "tenantId": "guid",
  "incidentId": "INC-001",
  "platform": "MDE",
  "action": "IsolateDevice",
  "parameters": {"deviceId": "guid"},
  "priority": "High",
  "initiatedBy": "soc@company.com",
  "justification": "Threat detected"
}
```

## Platforms

- MDE (61+ actions), MDO (16+), MCAS (15+), MDI (1+)
- EntraID (14+), Intune (15+), Azure (18+)

## License

MIT
