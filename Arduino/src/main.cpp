#include "I2Cdev.h"
#include "MPU6050_6Axis_MotionApps20.h"
#include <Arduino.h>
MPU6050 mpu;

#define TCAADDR 0x70
constexpr int MPU_COUNT = 7;

uint8_t fifoBuffer[45];         // FIFO Buffer

int16_t acceleration[3]{}; 

// Select IMU
void tcaselect(uint8_t i) {
	if (i > 7) return;
 
 	Wire.beginTransmission(TCAADDR);
 	Wire.write(1 << i);
 	Wire.endTransmission();
}

void setup() {
    Serial.begin(9600);
    Wire.begin();
	
  	for (uint8_t ch = 0; ch < MPU_COUNT; ch++) {
		tcaselect(ch);
		Serial.println(ch);
		mpu.initialize();
		mpu.setFullScaleAccelRange(MPU6050_ACCEL_FS_2);
  	mpu.setFullScaleGyroRange(MPU6050_GYRO_FS_250);
  	mpu.dmpInitialize();
		mpu.setRate(0x03); // 50Hz
  	mpu.setDMPEnabled(true);
	}
}



void loop() {
	for (uint8_t ch = 0; ch < MPU_COUNT; ch++) {
		tcaselect(ch);

		if(!mpu.dmpGetCurrentFIFOPacket(fifoBuffer)) {
			mpu.resetFIFO();
			continue;
		}

    Quaternion q;
    VectorFloat gravity;
    float ypr[3];

    mpu.dmpGetQuaternion(&q, fifoBuffer);
    mpu.dmpGetGravity(&gravity, &q);
    mpu.dmpGetYawPitchRoll(ypr, &q, &gravity);

		mpu.getAcceleration(&acceleration[2], &acceleration[1], &acceleration[0]); // x, y, z


		Serial.print(ch);
		Serial.print(' ');
    Serial.print((float)acceleration[2] / 32768 * 2, 6);
		Serial.print(' ');
    Serial.print((float)acceleration[1] / 32768 * 2, 6);
		Serial.print(' ');
    Serial.print((float)acceleration[0] / 32768 * 2, 6);
		Serial.print(' ');
    Serial.print(degrees(ypr[2]), 6);
		Serial.print(' ');
    Serial.print(degrees(ypr[1]), 6);
		Serial.print(' ');
    Serial.print(degrees(ypr[0]), 6);
		Serial.println();
  }
}
