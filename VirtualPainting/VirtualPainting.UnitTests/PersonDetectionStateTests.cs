using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VirtualPainting.UnitTests
{
    [TestClass]
    public class PersonDetectionStateTests
    {
        [TestMethod]
        public void PersonDetectionState_Equivalence()
        {
            var bodyPresenceArea = new Rect();
            var state = new PersonDetectionState(
                bodyIndex: 0,
                isPrimary: true,
                body: null,
                bodyPresenceArea: bodyPresenceArea);

            // Reflexive
            Assert.IsTrue(state.Equals(state));
#pragma warning disable CS1718 // Comparison made to same variable
            Assert.IsTrue(state == state);
#pragma warning restore CS1718 // Comparison made to same variable
            Assert.AreEqual(state, state);
            Assert.AreSame(state, state);

            var stateCopy1 = new PersonDetectionState(
                bodyIndex: 0,
                isPrimary: true,
                body: null,
                bodyPresenceArea: bodyPresenceArea);

            // Symmetric
            Assert.AreEqual(stateCopy1.Equals(state), state.Equals(stateCopy1));
            Assert.AreEqual(stateCopy1 == state, state == stateCopy1);

            // Transitive
            var stateCopy2 = new PersonDetectionState(
                bodyIndex: 0,
                isPrimary: true,
                body: null,
                bodyPresenceArea: bodyPresenceArea);

            Assert.AreEqual(state.Equals(stateCopy2), state.Equals(stateCopy1) && stateCopy1.Equals(stateCopy2));
            Assert.AreEqual(state == stateCopy2, state == stateCopy1 && stateCopy1 == stateCopy2);
        }

        [TestMethod]
        public void PersonDetectionState_Correctness()
        {
            var state = new PersonDetectionState(
                bodyIndex: 0,
                isPrimary: true,
                body: null,
                bodyPresenceArea: new Rect());

            Assert.IsFalse(state.Equals(null));
            Assert.IsFalse(state == null);
            Assert.AreNotEqual(null, state);
            Assert.AreNotSame(null, state);

            var stateCopy = new PersonDetectionState(
                bodyIndex: 1,
                isPrimary: false,
                body: null,
                bodyPresenceArea: new Rect());

            Assert.IsFalse(state.Equals(stateCopy));
            Assert.IsFalse(state == stateCopy);
            Assert.AreNotEqual(stateCopy, state);
            Assert.AreNotSame(stateCopy, state);
        }
    }
}
