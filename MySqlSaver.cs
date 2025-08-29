using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

public static class MySqlSaver
{
    // แก้ตามเครื่องของคุณ
    private const string ConnStr = "Server=172.16.200.202;Port=3307;Database=assetdb;Uid=root;Pwd=123456;";

    public static async Task<long> SaveSnapshotAsync(MachineSnapshot snap)
    {
        using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        using var tx = await conn.BeginTransactionAsync();

        // (ออปชัน) สร้างตารางถ้ายังไม่มี
        await EnsureTablesAsync(conn, tx);

        long machineId = await UpsertMachineAsync(conn, tx, snap.Machine);

        // เคลียร์ข้อมูลอุปกรณ์เดิมเพื่อแทนที่ด้วยรายการล่าสุด
        await ExecAsync(conn, tx, "DELETE FROM monitors WHERE machine_id=@id", ("@id", machineId));
        await ExecAsync(conn, tx, "DELETE FROM printers WHERE machine_id=@id", ("@id", machineId));
        await ExecAsync(conn, tx, "DELETE FROM scanners WHERE machine_id=@id", ("@id", machineId));
        await ExecAsync(conn, tx, "DELETE FROM card_readers WHERE machine_id=@id", ("@id", machineId));

        // แทรกใหม่
        foreach (var m in snap.Monitors)
        {
            await ExecAsync(conn, tx,
                @"INSERT INTO monitors (machine_id, manufacturer, model, serial)
                  VALUES (@mid, @mfg, @model, @serial)",
                ("@mid", machineId), ("@mfg", m.Manufacturer), ("@model", m.Model), ("@serial", m.Serial));
        }

        foreach (var p in snap.Printers)
        {
            await ExecAsync(conn, tx,
                @"INSERT INTO printers (machine_id, name, driver_name, port_name, is_network, is_default, is_shared, manufacturer)
                  VALUES (@mid, @name, @drv, @port, @net, @def, @shared, @mfg)",
                ("@mid", machineId),
                ("@name", p.Name),
                ("@drv", p.DriverName),
                ("@port", p.PortName),
                ("@net", p.IsNetwork ?? false),
                ("@def", p.IsDefault ?? false),
                ("@shared", p.IsShared ?? false),
                ("@mfg", p.Manufacturer)
            );
        }

        foreach (var s in snap.Scanners)
        {
            await ExecAsync(conn, tx,
                @"INSERT INTO scanners (machine_id, name, manufacturer, model, pnp_device_id)
                  VALUES (@mid, @name, @mfg, @model, @pnp)",
                ("@mid", machineId),
                ("@name", s.Name),
                ("@mfg", s.Manufacturer),
                ("@model", s.Model),
                ("@pnp", s.PnpDeviceId)
            );
        }

        foreach (var r in snap.CardReaders)
        {
            await ExecAsync(conn, tx,
                @"INSERT INTO card_readers (machine_id, name, manufacturer, model, pnp_device_id)
                  VALUES (@mid, @name, @mfg, @model, @pnp)",
                ("@mid", machineId),
                ("@name", r.Name),
                ("@mfg", r.Manufacturer),
                ("@model", r.Model),
                ("@pnp", r.PnpDeviceId)
            );
        }

        await tx.CommitAsync();
        return machineId;
    }

    private static async Task<long> UpsertMachineAsync(MySqlConnection conn, MySqlTransaction tx, MachineRecord m)
    {
        // ใช้ ON DUPLICATE KEY เพื่อคง id เดิมถ้ามี bios_serial หรือ machine_name ซ้ำ
        var sql = @"
INSERT INTO machines (machine_name, user_name, manufacturer, model, bios_serial, os_caption, os_version, os_arch)
VALUES (@name, @user, @mfg, @model, @serial, @osc, @osv, @arch)
ON DUPLICATE KEY UPDATE
  user_name=VALUES(user_name),
  manufacturer=VALUES(manufacturer),
  model=VALUES(model),
  os_caption=VALUES(os_caption),
  os_version=VALUES(os_version),
  os_arch=VALUES(os_arch),
  id=LAST_INSERT_ID(id);
SELECT LAST_INSERT_ID();";

        using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@name",  m.MachineName);
        cmd.Parameters.AddWithValue("@user",  (object?)m.UserName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mfg",   (object?)m.Manufacturer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@model", (object?)m.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@serial",(object?)m.BiosSerial ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@osc",   (object?)m.OsCaption ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@osv",   (object?)m.OsVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@arch",  (object?)m.OsArch ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private static async Task EnsureTablesAsync(MySqlConnection conn, MySqlTransaction tx)
    {
        // สร้างแบบย่อ (ถ้ามีอยู่แล้วคำสั่งจะข้าม)
        var ddl = @"
CREATE TABLE IF NOT EXISTS machines (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  machine_name VARCHAR(255) NOT NULL,
  user_name VARCHAR(255),
  manufacturer VARCHAR(255),
  model VARCHAR(255),
  bios_serial VARCHAR(120),
  os_caption VARCHAR(255),
  os_version VARCHAR(64),
  os_arch VARCHAR(32),
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_bios_serial (bios_serial),
  UNIQUE KEY uk_machine_name (machine_name)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS monitors (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  machine_id BIGINT NOT NULL,
  manufacturer VARCHAR(255),
  model VARCHAR(255),
  serial VARCHAR(255),
  FOREIGN KEY (machine_id) REFERENCES machines(id) ON DELETE CASCADE,
  INDEX ix_mon_machine (machine_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS printers (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  machine_id BIGINT NOT NULL,
  name VARCHAR(255),
  driver_name VARCHAR(255),
  port_name VARCHAR(255),
  is_network TINYINT(1),
  is_default TINYINT(1),
  is_shared TINYINT(1),
  manufacturer VARCHAR(255),
  FOREIGN KEY (machine_id) REFERENCES machines(id) ON DELETE CASCADE,
  INDEX ix_prn_machine (machine_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS scanners (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  machine_id BIGINT NOT NULL,
  name VARCHAR(255),
  manufacturer VARCHAR(255),
  model VARCHAR(255),
  pnp_device_id VARCHAR(500),
  FOREIGN KEY (machine_id) REFERENCES machines(id) ON DELETE CASCADE,
  INDEX ix_sc_machine (machine_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS card_readers (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  machine_id BIGINT NOT NULL,
  name VARCHAR(255),
  manufacturer VARCHAR(255),
  model VARCHAR(255),
  pnp_device_id VARCHAR(500),
  FOREIGN KEY (machine_id) REFERENCES machines(id) ON DELETE CASCADE,
  INDEX ix_cr_machine (machine_id)
) ENGINE=InnoDB;";

        using var cmd = new MySqlCommand(ddl, conn, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecAsync(MySqlConnection conn, MySqlTransaction tx, string sql, params (string, object?)[] ps)
    {
        using var cmd = new MySqlCommand(sql, conn, tx);
        foreach (var (k, v) in ps)
            cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
