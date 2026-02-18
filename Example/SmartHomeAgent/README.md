# Smart Home Automation Agent Example

This example demonstrates a realistic smart home automation system using UtilityAI to intelligently balance multiple competing priorities in real-time.

## Overview

The Smart Home Agent manages a modern connected home with IoT devices, optimizing for:
- **Energy Efficiency** - Reduce electricity costs while maintaining comfort
- **Security** - Protect the home and alert on anomalies
- **Comfort** - Maintain optimal temperature, lighting, and air quality
- **Maintenance** - Monitor device health and prevent failures

## Architecture

### Capability Modules (What the Agent CAN DO)

#### 1. EnergyManagementModule (Priority: 80)
Optimizes power consumption and reduces costs through intelligent load management.

**Strategies:**
- `energy.reduce_hvac_peak` - Temporarily reduce HVAC during peak pricing hours
- `energy.away_mode` - Power down non-essential devices when unoccupied
- `energy.precondition_offpeak` - Pre-heat/cool during cheaper off-peak hours
- `energy.defer_appliances` - Delay high-draw appliances (dishwasher, laundry) to off-peak

**Key Pattern:** Balances energy savings against comfort impact using considerations

#### 2. ComfortOptimizationModule (Priority: 90)
Maintains comfortable living conditions for occupants.

**Strategies:**
- `comfort.adjust_temperature` - HVAC control to reach target temperature
- `comfort.adjust_lighting` - Optimize brightness and color temperature by time of day
- `comfort.adjust_humidity` - Humidifier/dehumidifier control (ideal: 40-60%)
- `comfort.anticipate_weather` - Proactive adjustments based on weather forecast

**Key Pattern:** Prioritizes comfort when home is occupied, relaxes when away

#### 3. SecurityMonitoringModule (Priority: 100) ⚠️ HIGHEST
Protects the home and alerts on security events.

**Strategies:**
- `security.arm_system` - Activate alarm when leaving home
- `security.alert_unexpected_entry` - Critical alert on motion while armed
- `security.bedtime_check` - Verify all doors/windows closed before sleep
- `security.vacation_monitoring` - Enhanced surveillance + occupancy simulation

**Key Pattern:** Highest priority ensures security actions take precedence

#### 4. MaintenanceModule (Priority: 60)
Monitors device health and schedules maintenance proactively.

**Strategies:**
- `maintenance.battery_alert` - Warn when device batteries run low
- `maintenance.device_offline` - Attempt reconnection of unresponsive devices
- `maintenance.routine_service` - Schedule maintenance based on time since last service
- `maintenance.address_warnings` - Investigate and resolve device warning messages

**Key Pattern:** Selects most urgent device using domain logic, then proposes strategies

### Sensors (Observe & Publish Facts)

1. **EnvironmentSensor** - Indoor/outdoor temperature, humidity, occupancy detection, weather forecast
2. **EnergyMonitorSensor** - Power consumption, peak demand, time-of-use pricing
3. **SecuritySensor** - Door/window status, motion detection, alarm state
4. **DeviceHealthSensor** - Battery levels, device responsiveness, maintenance schedules

## How It Works

### The Orchestration Loop

Each tick (decision cycle), the system:

1. **SENSE** - Sensors observe environment and publish facts to EventBus
   - Current temperature, energy usage, security status, device health

2. **PROPOSE** - Capability modules propose strategies based on current facts
   - Each module yields 0-4 proposals depending on circumstances
   - Total: ~15-20 proposals evaluated per tick

3. **SCORE** - Utility system evaluates all proposals using considerations
   - Example: `comfort.adjust_temperature` scores high when:
     - Home is occupied (1.0)
     - Temperature deviation is large (quadratic curve)
     - Target is reasonable vs outdoor temp (0.3-1.0)

4. **SELECT** - Highest utility proposal wins
   - Security typically wins when away mode activated
   - Comfort wins during occupied hours
   - Energy optimization wins during peak pricing

5. **ACT** - Execute the winning proposal's action
   - Action publishes new facts to EventBus (e.g., `HomeAction`, `EnergyRecommendation`)
   - Next tick sees updated state

### Example Decision Flow

**Scenario:** Owner leaves home at 4:30 PM, peak electricity hours approaching

```
Tick 0: SENSE
  - Occupancy: away (3 minutes ago)
  - Security: NOT ARMED, kitchen window OPEN
  - Energy: 1200W @ $0.30/kWh
  - Temperature: 23.5°C (target: 21°C)

Tick 1: PROPOSE & SCORE
  security.arm_system                  0.850  ⬅ WINNER (high priority)
  energy.away_mode                     0.720
  comfort.adjust_temperature           0.250  (low - not home)

Tick 1: ACT
  ✅ Security system armed

Tick 2: PROPOSE & SCORE
  energy.away_mode                     0.850  ⬅ WINNER
  energy.reduce_hvac_peak              0.620
  maintenance.battery_alert            0.420

Tick 2: ACT
  ✅ Away mode enabled - $2.50/day savings

Tick 3: PROPOSE & SCORE
  maintenance.battery_alert            0.780  ⬅ WINNER
  energy.defer_appliances              0.520

Tick 3: ACT
  ⚠️ Battery alert: smoke_detector at 15%
```

## Key Patterns Demonstrated

### ✅ CORRECT: Capability-Based Modules

```csharp
// Module = ONE capability (energy management)
[Capability(Priority = 80, Domain = "energy")]
[RequiresFact<EnergyState>]
[RequiresFact<OccupancyPattern>]
public sealed class EnergyManagementModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Propose different STRATEGIES for managing energy
        yield return ProposalHelper.For("energy.reduce_hvac_peak")...
        yield return ProposalHelper.For("energy.away_mode")...
        yield return ProposalHelper.For("energy.defer_appliances")...
    }
}
```

### ✅ CORRECT: Domain-Specific Item Selection

```csharp
// MaintenanceModule: Select most urgent device FIRST
var devicesNeedingAttention = deviceHealthList
    .Where(d => !d.IsResponsive || d.BatteryLevel < 20 || d.Warnings.Any())
    .OrderByDescending(d => CalculateUrgency(d))
    .Take(3) // Focus on top 3
    .ToList();

var mostUrgent = devicesNeedingAttention.First();

// THEN propose strategies for THAT device
yield return ProposalHelper.For("maintenance.battery_alert")
    .WithConsideration(...) // Score based on mostUrgent
    .WithAction(async ct => await ReplaceDeviceBattery(mostUrgent, ct));
```

**Why this is correct:**
- Uses domain logic (urgency calculation) to select which device
- Proposes strategies for that device (battery vs reconnect vs service)
- Doesn't create 50 proposals (one per device) - that wastes CPU

### ✅ CORRECT: Declarative Considerations

```csharp
// NO if-statements in Propose() - use considerations instead
yield return ProposalHelper.For("energy.reduce_hvac_peak")
    .WithConsideration(new SignalConsideration<EnergyState>(
        name: "peak_hours",
        selector: e => e.IsPeakHours ? 1.0 : 0.0,
        curve: x => x,
        inputDomain: (0, 1)))
    .WithConsideration(new SignalConsideration<EnergyState>(
        name: "high_cost",
        selector: e => e.CostPerKwh,
        curve: x => x * x, // Quadratic - more urgent at high costs
        inputDomain: (0.10, 0.50)))
    .WithAction(...);
```

**Why this is correct:**
- Considerations make scoring transparent and debuggable
- Can be tuned without changing code
- Sinks can log consideration values for analysis

### ❌ WRONG: Device-Based Modules

```csharp
// ❌ BAD: One module per room/device
public class LivingRoomThermostatModule : ICapabilityModule { }
public class BedroomThermostatModule : ICapabilityModule { }
public class KitchenLightModule : ICapabilityModule { }
// ... 50 more modules

// This creates an explosion of modules that do the same thing!
```

### ❌ WRONG: Looping Through Devices as Proposals

```csharp
// ❌ BAD: Creates 50 proposals that differ only by device ID
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var devices = GetAllDevices(); // 50 devices

    foreach (var device in devices) // ❌ WRONG!
    {
        yield return ProposalHelper.For($"maintain.{device.Id}")
            .WithValue("urgency", device.BatteryLevel < 20 ? 1.0 : 0.0)
            .WithAction(async ct => await CheckDevice(device, ct));
    }
    // Wastes CPU scoring 50 similar proposals when only 1 executes
}
```

## Running the Example

```bash
cd Example/SmartHomeAgent
dotnet run
```

**Expected Output:**
```
🏠 Smart Home Automation Agent - Utility AI Example
═══════════════════════════════════════════════════════

📅 Scenario: Late Afternoon - Owner Just Left Home

📊 Initial System State:
  🏠 Occupancy: away
  🌡️  Temperature: 23.5°C (target: 21.0°C)
  ⚡ Power Usage: 1200W @ $0.30/kWh
  🔒 Security: NOT ARMED
  ⚠️  Open Windows: kitchen_window

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔄 Running Orchestration (10 ticks)...
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Tick 0] 🔍 Sensors updated, evaluating proposals...
[Tick 0] ⚖️  Top Proposals:
  🔒 security.arm_system                        | Utility: 0.892
  ⚡ energy.away_mode                           | Utility: 0.715
  🌡️ comfort.adjust_temperature                 | Utility: 0.283

[Tick 0] 🔒 WINNER: security.arm_system (utility=0.892)
    🔒 Security system armed (away mode)

[Tick 1] 🔍 Sensors updated, evaluating proposals...
[Tick 1] 🔒 WINNER: energy.away_mode (utility=0.843)
    🏠 Away mode activated - est. savings $2.50/day

...
```

## Interesting Behaviors to Observe

### 1. Priority Conflicts
Watch how the system balances competing goals:
- Security almost always wins when leaving home (highest priority)
- Energy savings activate once security is satisfied
- Comfort relaxes when away but stays active when home
- Maintenance deferred unless critical (low battery, device offline)

### 2. Dynamic Re-Prioritization
Utility scores change each tick based on:
- Time of day (peak vs off-peak pricing)
- Temperature drift (comfort becomes urgent)
- Battery drain (maintenance becomes critical)
- Security state changes

### 3. Emergent Behavior
No hardcoded "leaving home sequence" - behavior emerges:
1. Arm security (highest utility when away)
2. Enable energy savings (high utility after armed)
3. Reduce HVAC (high utility as peak hours approach)
4. Check device batteries (periodic, fills gaps)

### 4. Consideration Curves
Different response curves create different behaviors:
- **Linear** (`x => x`): Proportional response
- **Quadratic** (`x => x * x`): Accelerating urgency
- **Inverted** (`x => 1.0 - x`): Lower is better (cost, battery drain)

## Integration Ideas

This example can be extended with:

1. **Real IoT Devices** - Replace simulated sensors with actual device APIs:
   - Nest/Ecobee thermostat
   - Ring/SimpliSafe security
   - Philips Hue lighting
   - TP-Link smart plugs

2. **LLM Integration** - Add natural language control:
   ```csharp
   [Capability(Priority = 85)]
   public class NaturalLanguageModule : ICapabilityModule
   {
       // "Hey assistant, I'm cold" → adjust temperature
       // "Set vacation mode" → arm security, enable simulation
   }
   ```

3. **Predictive Learning** - Learn occupancy patterns:
   - Track when users typically leave/return
   - Predict temperature preferences by time of day
   - Optimize pre-conditioning timing

4. **Mobile App Integration** - Send notifications via push:
   ```csharp
   bus.Subscribe<HomeNotification>(async notification =>
   {
       if (notification.Priority == "critical")
           await SendPushNotification(notification.Message);
   });
   ```

## Comparison with AgentAssistant Example

| Aspect | SmartHomeAgent | AgentAssistant |
|--------|----------------|----------------|
| **Domain** | IoT/Home Automation | Conversational AI |
| **Sensors** | Environmental, Energy, Security | Message history, Intent |
| **Priorities** | Security > Comfort > Energy | User satisfaction, Accuracy |
| **Decision Speed** | Continuous (every tick) | Event-driven (per message) |
| **State Complexity** | High (many devices) | Medium (conversation context) |

## See Also

- [PROPOSAL_PATTERNS.md](../../docs/PROPOSAL_PATTERNS.md) - Detailed pattern guide
- [INTEGRATION.md](../../docs/INTEGRATION.md) - Real device integration
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - Framework deep dive
- [AgentAssistant Example](../AgentAssistant/) - Conversational AI use case
