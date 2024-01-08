using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Testing : MonoBehaviour {

    // Start is called before the first frame update
    void Start() {
    }

    // Update is called once per frame
    void Update() {

    }

    #region Config Test
    struct TestStruct1 {
        public byte a;
        public byte r;
        public byte g;
        public byte b;

        public override string ToString() {
            return string.Format("{{ a: {0:d}, r: {1:d}, g: {2:d}, b: {3:d} }}", a, r, g, b);
        }
    }

    struct TestStruct2 {
        public byte a;
        public int b;
        public byte c;
        public long d; public override string ToString() {
            return string.Format("{{ a: {0:d}, b: {1:d}, c: {2:d}, d: {3:d} }}", a, b, c, d);
        }
    }

    void TestSetGet<T>(string id, T value) where T : unmanaged {
        Config.Set(id, value);
        Debug.LogFormat("{0:s}: {1:g}", id, Config.Get<T>(id));
    }

    void TestSetGet(string id, string value) {
        Config.Set(id, value);
        Config.Get(id, out string str);
        Debug.LogFormat("{0:s}: {1:s}", id, str);
    }

    void TestConfig() {
        TestSetGet("string test", "test string value");
        TestSetGet("byte test", (byte)77);
        TestSetGet("int test", 6541655);
        TestSetGet("long test", 654165658755);
        
        TestStruct1 testStruct1 = new TestStruct1();
        testStruct1.a = 250;
        testStruct1.r = 200;
        testStruct1.g = 150;
        testStruct1.b = 100;
        TestStruct2 testStruct2 = new TestStruct2();
        testStruct2.a = 255;
        testStruct2.b = 87465135;
        testStruct2.c = 150;
        testStruct2.d = 3168463513566;

        TestSetGet("test struct 1", testStruct1);
        TestSetGet("test struct 2", testStruct2);
    }
    #endregion
}
