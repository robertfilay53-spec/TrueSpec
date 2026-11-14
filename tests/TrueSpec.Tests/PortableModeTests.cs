using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// Portable mode switched from inside the app (v2.1.1): the marker file
/// next to the exe is what the Settings page creates and removes. Pure
/// file I/O against temp folders, so these run on both legs.
/// </summary>
public class PortableModeTests
{
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string MarkerPath(string exeDir) =>
        Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName);

    [Fact]
    public void GetStatus_NoMarkerNoEnv_IsNotPortableInDefaultFolder()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: null);

            Assert.False(status.IsPortable);
            Assert.False(status.MarkerPresent);
            Assert.False(status.EnvRequested);
            Assert.Equal(PathSafety.DefaultAppDataFolder(), status.DataFolder);
            Assert.Equal(Path.GetFullPath(exeDir), status.ExeFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_MarkerPresent_IsPortableNextToExe()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: null);

            Assert.True(status.IsPortable);
            Assert.True(status.MarkerPresent);
            Assert.False(status.EnvRequested);
            Assert.Equal(Path.GetFullPath(exeDir), status.DataFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_EnvVar_IsPortableWithoutMarker()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: "1");

            Assert.True(status.IsPortable);
            Assert.False(status.MarkerPresent);
            Assert.True(status.EnvRequested);
            Assert.Equal(Path.GetFullPath(exeDir), status.DataFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_EnvVarOff_IsNotPortable()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: "0");

            Assert.False(status.IsPortable);
            Assert.False(status.EnvRequested);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_UnusableExeFolder_FallsBackToDefault()
    {
        var status = PortableMode.GetStatus(exeDirectory: string.Empty, environmentValue: null);

        Assert.False(status.IsPortable);
        Assert.Equal(PathSafety.DefaultAppDataFolder(), status.DataFolder);
    }

    [Fact]
    public void SetEnabled_Enable_CreatesMarkerAndReportsRestart()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.True(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);

            var status = PortableMode.GetStatus(exeDir, environmentValue: null);
            Assert.True(status.IsPortable);
            Assert.True(status.MarkerPresent);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_AlreadyOn_IsANoopSuccess()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Contains("already on", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_MissingExeFolder_FailsHonestly()
    {
        var missing = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "no-such-dir");

        var result = PortableMode.SetEnabled(missing, enable: true);

        Assert.False(result.Success);
        Assert.False(result.IsPortable);
        Assert.Contains("could not be enabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetEnabled_Enable_MarkerPathIsADirectory_FailsHonestly()
    {
        var exeDir = TempExeDir();
        Directory.CreateDirectory(MarkerPath(exeDir));

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.False(result.Success);
            Assert.False(result.IsPortable);
            Assert.Contains("could not be enabled", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_ProtectedFolder_IsRefused()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows))
        {
            return;
        }

        var result = PortableMode.SetEnabled(windows, enable: true);

        Assert.False(result.Success);
        Assert.False(result.IsPortable);
        Assert.Contains("system or protected folder", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(MarkerPath(windows)));
    }

    [Fact]
    public void SetEnabled_Disable_RemovesMarker()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false);

            Assert.True(result.Success);
            Assert.False(result.IsPortable);
            Assert.False(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_AlreadyOff_IsANoopSuccess()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false);

            Assert.True(result.Success);
            Assert.False(result.IsPortable);
            Assert.Contains("already off", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_EnvVarStillRequests_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "1");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.False(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("TRUESPEC_PORTABLE", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_NoMarkerButEnvVar_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "yes");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Contains("TRUESPEC_PORTABLE", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GermanLanguage_ReturnsGermanMessages()
    {
        // Localisation, phase 3 (v2.1.9): the toggle takes a language
        // code and its outcome message comes back in it. The default
        // (no language) stays English, as the tests above pin.
        var exeDir = TempExeDir();

        try
        {
            var enabled = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "de");
            Assert.True(enabled.Success);
            Assert.True(enabled.IsPortable);
            Assert.Equal("Der tragbare Modus ist an. Starten Sie TrueSpec neu: Von da an werden Einstellungen, Verlauf und Sensorprotokoll neben der App gespeichert statt im Standardordner. Bereits gespeicherte Scans bleiben im alten Ordner.", enabled.Message);

            var alreadyOn = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "de");
            Assert.True(alreadyOn.Success);
            Assert.Equal("Der tragbare Modus ist bereits an. Starten Sie TrueSpec neu, damit er wirksam wird.", alreadyOn.Message);

            var disabled = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "de");
            Assert.True(disabled.Success);
            Assert.False(disabled.IsPortable);
            Assert.Equal("Der tragbare Modus ist aus. Starten Sie TrueSpec neu: Von da an werden Daten im Standardordner gespeichert. Bereits neben der App gespeicherte Scans bleiben dort.", disabled.Message);

            var alreadyOff = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "de");
            Assert.True(alreadyOff.Success);
            Assert.Equal("Der tragbare Modus ist bereits aus.", alreadyOff.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GermanLanguage_NoMarkerButEnvVar_NamesEnvVarInGerman()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "yes", languageCode: "de");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Equal("Es gibt keine Marker-Datei zu entfernen: Der tragbare Modus ist über die Umgebungsvariable TRUESPEC_PORTABLE an. Heben Sie die Variable auf, um ihn auszuschalten.", result.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void FrenchLanguage_ReturnsFrenchMessages()
    {
        // Localisation, phase 3, French (v2.1.22): the toggle takes a
        // language code and its outcome message comes back in it —
        // mirror of GermanLanguage_ReturnsGermanMessages. The default
        // (no language) stays English, as the tests above pin.
        var exeDir = TempExeDir();

        try
        {
            var enabled = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "fr");
            Assert.True(enabled.Success);
            Assert.True(enabled.IsPortable);
            Assert.Equal("Le mode portable est activé. Redémarrez TrueSpec : à partir de là, les paramètres, l'historique et le journal des capteurs sont conservés à côté de l'application au lieu du dossier par défaut. Les scans déjà enregistrés restent dans l'ancien dossier.", enabled.Message);

            var alreadyOn = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "fr");
            Assert.True(alreadyOn.Success);
            Assert.Equal("Le mode portable est déjà activé. Redémarrez TrueSpec pour qu'il prenne effet.", alreadyOn.Message);

            var disabled = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "fr");
            Assert.True(disabled.Success);
            Assert.False(disabled.IsPortable);
            Assert.Equal("Le mode portable est désactivé. Redémarrez TrueSpec : à partir de là, les données sont conservées dans le dossier par défaut. Les scans déjà enregistrés à côté de l'application restent là.", disabled.Message);

            var alreadyOff = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "fr");
            Assert.True(alreadyOff.Success);
            Assert.Equal("Le mode portable est déjà désactivé.", alreadyOff.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void FrenchLanguage_NoMarkerButEnvVar_NamesEnvVarInFrench()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "yes", languageCode: "fr");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Equal("Il n'y a aucun fichier marqueur à supprimer : le mode portable est activé via la variable d'environnement TRUESPEC_PORTABLE. Désactivez la variable pour le désactiver.", result.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SpanishLanguage_ReturnsSpanishMessages()
    {
        // Localisation, phase 3, Spanish (v2.1.29): the toggle takes a
        // language code and its outcome message comes back in it —
        // mirror of FrenchLanguage_ReturnsFrenchMessages. The default
        // (no language) stays English, as the tests above pin. Values
        // read from the real Spanish pack with the scratch dump program
        // (~/workspace/es-pack-dump/).
        var exeDir = TempExeDir();

        try
        {
            var enabled = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "es");
            Assert.True(enabled.Success);
            Assert.True(enabled.IsPortable);
            Assert.Equal("El modo portátil está activado. Reinicia TrueSpec: a partir de entonces, la configuración, el historial y el registro de sensores se guardan junto a la aplicación en lugar de en la carpeta predeterminada. Los análisis ya guardados permanecen en la carpeta anterior.", enabled.Message);

            var alreadyOn = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "es");
            Assert.True(alreadyOn.Success);
            Assert.Equal("El modo portátil ya está activado. Reinicia TrueSpec para que surta efecto.", alreadyOn.Message);

            var disabled = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "es");
            Assert.True(disabled.Success);
            Assert.False(disabled.IsPortable);
            Assert.Equal("El modo portátil está desactivado. Reinicia TrueSpec: a partir de entonces, los datos se guardan en la carpeta predeterminada. Los análisis ya guardados junto a la aplicación permanecen allí.", disabled.Message);

            var alreadyOff = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: null, languageCode: "es");
            Assert.True(alreadyOff.Success);
            Assert.Equal("El modo portátil ya está desactivado.", alreadyOff.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SpanishLanguage_NoMarkerButEnvVar_NamesEnvVarInSpanish()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "yes", languageCode: "es");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Equal("No hay ningún archivo marcador que eliminar: el modo portátil está activado mediante la variable de entorno TRUESPEC_PORTABLE. Anula la variable para desactivarlo.", result.Message);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglishMessage()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true, environmentValue: null, languageCode: "xx");

            Assert.True(result.Success);
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void ToggleResult_MessageCanBeReResolvedInAnotherLanguage()
    {
        // The result carries its message as a key, so the Settings page
        // can re-resolve it when the language changes without toggling
        // again: a `with` copy in German reads German.
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);
            var german = result with { LanguageCode = "de" };
            Assert.Equal("Der tragbare Modus ist an. Starten Sie TrueSpec neu: Von da an werden Einstellungen, Verlauf und Sensorprotokoll neben der App gespeichert statt im Standardordner. Bereits gespeicherte Scans bleiben im alten Ordner.", german.Message);
            // A `with` copy in French reads French too (v2.1.22).
            var french = result with { LanguageCode = "fr" };
            Assert.Equal("Le mode portable est activé. Redémarrez TrueSpec : à partir de là, les paramètres, l'historique et le journal des capteurs sont conservés à côté de l'application au lieu du dossier par défaut. Les scans déjà enregistrés restent dans l'ancien dossier.", french.Message);
            // A `with` copy in Spanish reads Spanish too (v2.1.29).
            var spanish = result with { LanguageCode = "es" };
            Assert.Equal("El modo portátil está activado. Reinicia TrueSpec: a partir de entonces, la configuración, el historial y el registro de sensores se guardan junto a la aplicación en lugar de en la carpeta predeterminada. Los análisis ya guardados permanecen en la carpeta anterior.", spanish.Message);
            // The original is untouched: a `with` copy never mutates.
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }
}
