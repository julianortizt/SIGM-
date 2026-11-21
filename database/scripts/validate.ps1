#Requires -Version 5.1
<#
.SYNOPSIS
    Valida el modelo de datos de SIGM Cloud en PostgreSQL 18.

.DESCRIPTION
    Ejecuta las migraciones, los seeds oficiales y las pruebas sobre una base
    temporal. Admite tres modos:

    - LocalServer : usa una instancia PostgreSQL ya en ejecucion y crea una base
                    de validacion aislada. Es el modo mas rapido y no depende de
                    Docker ni de initdb.
    - LocalCluster: crea un cluster efimero con initdb/pg_ctl.
    - Docker      : usa un contenedor temporal con la imagen postgres:18.

    En todos los modos ejecuta run_all.sql dos veces para comprobar que las
    migraciones y los seeds son idempotentes.

.PARAMETER Mode
    Auto (por defecto): LocalServer si hay una instancia accesible; si no,
    LocalCluster; si no, Docker.

.PARAMETER Port
    Puerto para el cluster temporal o el contenedor. 0 elige un puerto libre.

.PARAMETER ServerHost
    Host de la instancia existente (modo LocalServer).

.PARAMETER ServerPort
    Puerto de la instancia existente (modo LocalServer).

.PARAMETER ServerUser
    Usuario superusuario de la instancia existente.

.PARAMETER ServerPassword
    Clave del usuario. Si se omite se usa la variable PGPASSWORD.

.PARAMETER PgBin
    Directorio que contiene initdb.exe, pg_ctl.exe y psql.exe.

.PARAMETER KeepCluster
    Conserva el cluster o la base de validacion al terminar (diagnostico).

.EXAMPLE
    .\scripts\validate.ps1 -Mode LocalServer -ServerPassword postgres

.EXAMPLE
    .\scripts\validate.ps1 -Mode LocalCluster -KeepCluster
#>
[CmdletBinding()]
param(
    [ValidateSet('Auto', 'LocalServer', 'LocalCluster', 'Docker')]
    [string]$Mode = 'Auto',

    [int]$Port = 0,

    [string]$ServerHost = '127.0.0.1',

    [int]$ServerPort = 5432,

    [string]$ServerUser = 'postgres',

    [string]$ServerPassword = $env:PGPASSWORD,

    [string]$DatabaseName = 'sigm_cloud_validate',

    [string]$PgBin = '',

    [switch]$KeepCluster
)

$ErrorActionPreference = 'Stop'
$script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:RunAll = Join-Path $script:Root 'run_all.sql'
$script:Tests = @(
    (Join-Path $script:Root 'tests\001_rls_test.sql'),
    (Join-Path $script:Root 'tests\002_smoke_test.sql'),
    (Join-Path $script:Root 'tests\003_catalog_test.sql'),
    (Join-Path $script:Root 'tests\004_rls_coverage_test.sql')
)
$script:PreviousPgPassword = $env:PGPASSWORD

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Test-NativeSuccess([string]$Description) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Description fallo con codigo de salida $LASTEXITCODE."
    }
}

function Find-PgBin {
    param([string]$Explicit)

    if ($Explicit) {
        if (Test-Path (Join-Path $Explicit 'initdb.exe')) {
            return (Resolve-Path $Explicit).Path
        }
        throw "PgBin no contiene initdb.exe: $Explicit"
    }

    if ($env:PG_BIN -and (Test-Path (Join-Path $env:PG_BIN 'initdb.exe'))) {
        return (Resolve-Path $env:PG_BIN).Path
    }

    $command = Get-Command 'psql.exe' -ErrorAction SilentlyContinue
    if ($command) {
        $candidate = Split-Path $command.Source -Parent
        if (Test-Path (Join-Path $candidate 'initdb.exe')) {
            return $candidate
        }
    }

    $base = 'C:\Program Files\PostgreSQL'
    if (Test-Path $base) {
        $directories = Get-ChildItem $base -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending
        foreach ($directory in $directories) {
            $candidate = Join-Path $directory.FullName 'bin'
            if (Test-Path (Join-Path $candidate 'initdb.exe')) {
                return $candidate
            }
        }
    }

    return $null
}

function Get-FreePort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Test-DockerAvailable {
    try {
        $null = docker version --format '{{.Server.Version}}' 2>&1
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

function Test-ServerAvailable {
    param([string]$Bin)

    if (-not $Bin) { return $false }
    $previousEap = $ErrorActionPreference
    $previousPassword = $env:PGPASSWORD
    $ErrorActionPreference = 'Continue'
    if ($ServerPassword) { $env:PGPASSWORD = $ServerPassword }
    try {
        $isready = Join-Path $Bin 'pg_isready.exe'
        $psql = Join-Path $Bin 'psql.exe'
        & $isready -h $ServerHost -p $ServerPort -U $ServerUser *> $null
        if ($LASTEXITCODE -ne 0) { return $false }

        & $psql -X -h $ServerHost -p $ServerPort -U $ServerUser -d postgres -Atc 'select 1' *> $null
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        $env:PGPASSWORD = $previousPassword
        $ErrorActionPreference = $previousEap
    }
}

function Invoke-SqlFile {
    param(
        [string]$Psql,
        [string]$SqlHost,
        [int]$PortNumber,
        [string]$SqlUser,
        [string]$Database,
        [string]$File
    )

    if (-not (Test-Path $File)) {
        throw "No existe el archivo SQL: $File"
    }

    Write-Host "  psql -f $([System.IO.Path]::GetFileName($File))"
    & $Psql -X -q -v ON_ERROR_STOP=1 -h $SqlHost -p $PortNumber -U $SqlUser -d $Database -f $File
    Test-NativeSuccess "La ejecucion de $([System.IO.Path]::GetFileName($File))"
}

function Invoke-ModelValidation {
    param(
        [string]$Psql,
        [string]$SqlHost,
        [int]$PortNumber,
        [string]$SqlUser,
        [string]$Database
    )

    Write-Step 'Migraciones, catalogos oficiales y RLS'
    Invoke-SqlFile -Psql $Psql -SqlHost $SqlHost -PortNumber $PortNumber `
        -SqlUser $SqlUser -Database $Database -File $script:RunAll

    Write-Step 'Re-ejecucion idempotente de run_all.sql'
    Invoke-SqlFile -Psql $Psql -SqlHost $SqlHost -PortNumber $PortNumber `
        -SqlUser $SqlUser -Database $Database -File $script:RunAll

    foreach ($test in $script:Tests) {
        Write-Step ('Prueba ' + [System.IO.Path]::GetFileName($test))
        Invoke-SqlFile -Psql $Psql -SqlHost $SqlHost -PortNumber $PortNumber `
            -SqlUser $SqlUser -Database $Database -File $test
    }

    Write-Step 'Validacion completada correctamente'
}

function Invoke-LocalServer {
    $bin = Find-PgBin -Explicit $PgBin
    if (-not $bin) {
        throw 'No se encontro PostgreSQL. Use -PgBin o instale PostgreSQL 18.'
    }
    if (-not $ServerPassword) {
        throw 'Modo LocalServer requiere -ServerPassword o la variable PGPASSWORD.'
    }

    # PowerShell 5.1 convierte los NOTICE de stderr en errores terminantes cuando
    # ErrorActionPreference es Stop. Los comandos criticos se validan con
    # Test-NativeSuccess y el codigo de salida de cada utilidad.
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    $psql = Join-Path $bin 'psql.exe'
    $dropdb = Join-Path $bin 'dropdb.exe'
    $createdb = Join-Path $bin 'createdb.exe'
    $env:PGPASSWORD = $ServerPassword

    # Solo se eliminan los roles que este script cree. Si ya existian, se conservan.
    $rolesBefore = & $psql -X -h $ServerHost -p $ServerPort -U $ServerUser -d postgres -Atc `
        "select rolname from pg_roles where rolname in ('sigm_app','sigm_platform')" 2>$null
    $rolesExisted = @($rolesBefore | Where-Object { $_ })

    Write-Step "PostgreSQL local $ServerHost`:$ServerPort (base $DatabaseName)"
    try {
        $exists = & $psql -X -h $ServerHost -p $ServerPort -U $ServerUser -d postgres -Atc `
            "select 1 from pg_database where datname = '$DatabaseName'" 2>$null
        if ($exists) {
            & $dropdb -h $ServerHost -p $ServerPort -U $ServerUser $DatabaseName 2>&1 | Out-Null
            Test-NativeSuccess 'dropdb'
        }

        & $createdb -h $ServerHost -p $ServerPort -U $ServerUser $DatabaseName 2>&1 | Out-Null
        Test-NativeSuccess 'createdb'

        Invoke-ModelValidation -Psql $psql -SqlHost $ServerHost -PortNumber $ServerPort `
            -SqlUser $ServerUser -Database $DatabaseName
    }
    finally {
        $ErrorActionPreference = 'Continue'
        if (-not $KeepCluster) {
            Write-Host ''
            Write-Host 'Eliminando la base de validacion...'
            $exists = & $psql -X -h $ServerHost -p $ServerPort -U $ServerUser -d postgres -Atc `
                "select 1 from pg_database where datname = '$DatabaseName'" 2>$null
            if ($exists) {
                & $dropdb -h $ServerHost -p $ServerPort -U $ServerUser $DatabaseName 2>&1 | Out-Null
            }

            if ($rolesExisted.Count -eq 0) {
                & $psql -X -h $ServerHost -p $ServerPort -U $ServerUser -d postgres -c `
                    'DROP ROLE IF EXISTS sigm_app; DROP ROLE IF EXISTS sigm_platform;' 2>&1 | Out-Null
            }
        }
        else {
            Write-Host "Base conservada: $DatabaseName" -ForegroundColor Yellow
        }
        $ErrorActionPreference = $previousEap
    }
}

function Invoke-LocalCluster {
    $bin = Find-PgBin -Explicit $PgBin
    if (-not $bin) {
        throw 'No se encontro PostgreSQL. Instale PostgreSQL 18 o use -Mode Docker.'
    }

    if ($Port -le 0) {
        $Port = Get-FreePort
    }

    $initdb = Join-Path $bin 'initdb.exe'
    $pgctl = Join-Path $bin 'pg_ctl.exe'
    $psql = Join-Path $bin 'psql.exe'
    $isready = Join-Path $bin 'pg_isready.exe'

    $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ('sigm-validate-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $data = Join-Path $workspace 'pgdata'
    $log = Join-Path $workspace 'postgres.log'
    New-Item -ItemType Directory -Path $workspace -Force | Out-Null

    $started = $false
    try {
        Write-Step "PostgreSQL local temporal (puerto $Port)"
        Write-Host "  Binarios: $bin"
        Write-Host "  Datos   : $workspace"

        & $initdb -D $data -U postgres -A trust -E UTF8 --locale=C --no-sync | Out-Null
        Test-NativeSuccess 'initdb'

        & $pgctl -D $data -l $log -o "-p $Port -c listen_addresses=127.0.0.1 -c fsync=off -c max_connections=100" -w start | Out-Null
        Test-NativeSuccess 'pg_ctl start'
        $started = $true

        $ready = $false
        for ($i = 0; $i -lt 30; $i++) {
            & $isready -h 127.0.0.1 -p $Port -U postgres *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) {
            if (Test-Path $log) { Get-Content $log -Tail 40 }
            throw 'PostgreSQL local no estuvo listo en el tiempo esperado.'
        }

        Invoke-ModelValidation -Psql $psql -SqlHost '127.0.0.1' -PortNumber $Port `
            -SqlUser 'postgres' -Database $DatabaseName
    }
    finally {
        $previousEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        if ($started -and (Test-Path (Join-Path $data 'PG_VERSION'))) {
            Write-Host ''
            Write-Host 'Deteniendo el cluster temporal...'
            & $pgctl -D $data -m fast -w stop 2>&1 | Out-Null
        }

        if ($KeepCluster) {
            Write-Host "Cluster conservado en: $workspace" -ForegroundColor Yellow
            if (Test-Path $log) {
                Write-Host "Log: $log"
            }
        }
        else {
            Remove-Item $workspace -Recurse -Force -ErrorAction SilentlyContinue
        }
        $ErrorActionPreference = $previousEap
    }
}

function Invoke-DockerValidation {
    $containerName = 'sigm-postgres-test'

    Write-Step 'PostgreSQL 18 en Docker'
    $existing = docker ps -a --filter "name=^/$containerName$" --format '{{.Names}}' 2>$null
    if ($existing) {
        Write-Host '  Eliminando contenedor anterior...'
        docker rm -f $containerName | Out-Null
    }

    if ($Port -le 0) {
        $Port = Get-FreePort
    }

    docker run --rm -d `
        --name $containerName `
        -e POSTGRES_PASSWORD=sigm_test `
        -e POSTGRES_DB=$DatabaseName `
        -p "${Port}:5432" `
        -v "${script:Root}:/db" `
        postgres:18 | Out-Null
    Test-NativeSuccess 'docker run'

    try {
        Write-Host '  Esperando disponibilidad de PostgreSQL...'
        $ready = $false
        for ($i = 0; $i -lt 60; $i++) {
            docker exec $containerName pg_isready -U postgres -d $DatabaseName *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) {
            docker logs $containerName
            throw 'PostgreSQL Docker no estuvo listo en el tiempo esperado.'
        }

        foreach ($step in @('run_all.sql', 'run_all.sql (idempotente)')) {
            Write-Step $step
            docker exec $containerName psql -X -U postgres -d $DatabaseName -v ON_ERROR_STOP=1 -f /db/run_all.sql
            Test-NativeSuccess $step
        }

        foreach ($test in $script:Tests) {
            $name = [System.IO.Path]::GetFileName($test)
            Write-Step "Prueba $name"
            docker exec $containerName psql -X -U postgres -d $DatabaseName -v ON_ERROR_STOP=1 -f "/db/tests/$name"
            Test-NativeSuccess $name
        }

        Write-Step 'Validacion completada correctamente'
    }
    finally {
        Write-Host ''
        Write-Host 'Eliminando contenedor de prueba...'
        docker rm -f $containerName *> $null
    }
}

try {
    Write-Host 'SIGM Cloud :: validacion del modelo de datos' -ForegroundColor Cyan
    Write-Host "Directorio: $script:Root"

    switch ($Mode) {
        'LocalServer' {
            Invoke-LocalServer
        }
        'LocalCluster' {
            Invoke-LocalCluster
        }
        'Docker' {
            Invoke-DockerValidation
        }
        default {
            $bin = Find-PgBin -Explicit $PgBin
            if ($ServerPassword -and (Test-ServerAvailable -Bin $bin)) {
                Invoke-LocalServer
            }
            elseif ($bin) {
                Invoke-LocalCluster
            }
            elseif (Test-DockerAvailable) {
                Invoke-DockerValidation
            }
            else {
                throw 'No se encontro PostgreSQL local ni Docker. Use -PgBin o instale PostgreSQL 18.'
            }
        }
    }
}
finally {
    $env:PGPASSWORD = $script:PreviousPgPassword
}
