using System;
using System.Reflection;
using NUnit.Framework;

namespace PulseForge.Tests.EditMode
{
    public sealed class PulseForgeInputBindingMigrationTests
    {
        [Test]
        public void LegacyStrikeOverrideNameMigratesWithoutChangingOtherBindings()
        {
            Type serviceType = Type.GetType(
                "PulseForge.Runtime.Unity.Input.PulseForgeInputService, Assembly-CSharp",
                true);
            MethodInfo migrate = serviceType.GetMethod(
                "MigrateLegacyBindingOverridesJson",
                BindingFlags.Public | BindingFlags.Static);
            const string source =
                "{\"bindings\":[{\"action\":\"PulseForge Gameplay/Strike\",\"path\":\"<Keyboard>/u\"},"
                + "{\"action\":\"PulseForge Gameplay/Guard\",\"path\":\"<Keyboard>/g\"}]}";

            string result = (string)migrate.Invoke(null, new object[] { source });

            Assert.That(result, Does.Contain("\"action\":\"PulseForge Gameplay/LightAttack\""));
            Assert.That(result, Does.Contain("\"action\":\"PulseForge Gameplay/Guard\""));
            Assert.That(result, Does.Contain("<Keyboard>/g"));
            Assert.That(result, Does.Not.Contain("/Strike\""));
        }
    }
}
