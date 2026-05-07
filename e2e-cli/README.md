# Analytics-CSharp e2e-cli

A small CLI tool used by the [sdk-e2e-tests](https://github.com/segmentio/sdk-e2e-tests) framework to run end-to-end tests against the Analytics-CSharp SDK.

## Prerequisites

- .NET 6 SDK or later
- Node.js 18+ (only needed when running the full test suite via `run-e2e.sh`)

## Build

```bash
cd e2e-cli
dotnet build -c Release -o build
```

## Usage

```bash
dotnet build/e2e-cli.dll --input '<json>'
```

The CLI reads a single `--input` argument containing a JSON string that describes the SDK configuration and event sequences to send.

### Input JSON format

```json
{
  "writeKey": "YOUR_WRITE_KEY",
  "apiHost": "https://api.segment.io/v1",
  "sequences": [
    {
      "delayMs": 0,
      "events": [
        {"type": "identify", "userId": "user-1", "traits": {"name": "Alice"}},
        {"type": "track",    "userId": "user-1", "event": "Button Clicked", "properties": {"button": "signup"}},
        {"type": "page",     "userId": "user-1", "name": "Home", "category": "Nav"},
        {"type": "screen",   "userId": "user-1", "name": "Main"},
        {"type": "alias",    "userId": "new-id", "previousId": "old-id"},
        {"type": "group",    "userId": "user-1", "groupId": "group-1", "traits": {"plan": "pro"}}
      ]
    }
  ],
  "config": {
    "flushAt": 15,
    "flushInterval": 1000,
    "maxRetries": 3,
    "timeout": 10
  }
}
```

#### Top-level fields

| Field      | Type   | Required | Description |
|------------|--------|----------|-------------|
| `writeKey` | string | yes      | Segment source write key |
| `apiHost`  | string | no       | Override the Segment API host (e.g. a local proxy) |
| `sequences`| array  | yes      | Ordered list of event sequences |
| `config`   | object | no       | SDK tuning parameters (see below) |

#### `config` fields

| Field           | Type | Default | Description |
|-----------------|------|---------|-------------|
| `flushAt`       | int  | 15      | Flush after this many events |
| `flushInterval` | int  | 10000   | Flush interval in **milliseconds** |
| `maxRetries`    | int  | 3       | (informational, not yet wired to SDK) |
| `timeout`       | int  | 10      | (informational) |

#### Event fields

All events share a `type` field. Additional fields per type:

| Type       | Required fields          | Optional fields |
|------------|--------------------------|-----------------|
| `identify` | `userId`                 | `traits` (object) |
| `track`    | `userId`, `event`        | `properties` (object) |
| `page`     | `userId`, `name`         | `category`, `properties` |
| `screen`   | `userId`, `name`         | `category`, `properties` |
| `alias`    | `userId` (new id), `previousId` | — |
| `group`    | `userId`, `groupId`      | `traits` (object) |

### Output JSON format

Written to **stdout** on the last line:

```json
{"success": true, "sentBatches": 1}
```

On failure:

```json
{"success": false, "sentBatches": 0, "error": "description of the error"}
```

The process exits with code `0` on success and `1` on failure.

Debug information is written to **stderr**.

## Running the full E2E test suite

```bash
# Clone the test framework next to this repo
git clone https://github.com/segmentio/sdk-e2e-tests ../sdk-e2e-tests

# Run all suites defined in e2e-config.json
./e2e-cli/run-e2e.sh

# Pass extra arguments to run-tests.sh
./e2e-cli/run-e2e.sh --suite basic

# Use a custom test-framework location
E2E_TESTS_DIR=/path/to/sdk-e2e-tests ./e2e-cli/run-e2e.sh
```
