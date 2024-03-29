﻿//--------------------- Copyright Block ----------------------
/*

PrayTime.cs: Prayer Times Calculator (ver 1.2)
Copyright (C) 2007-2010 PrayTimes.org

C# Code By: Jandost Khoso
Original JS Code By: Hamid Zarrabi-Zadeh

License: GNU LGPL v3.0

TERMS OF USE:
	Permission is granted to use this code, with or
	without modification, in any website or application
	provided that credit is given to the original work
	with a link back to PrayTimes.org.

This program is distributed in the hope that it will
be useful, but WITHOUT ANY WARRANTY.

PLEASE DO NOT REMOVE THIS COPYRIGHT BLOCK.

*/

using System;
using System.Collections.Generic;

namespace Muazun.NamazTime
{

    public class NamazTimeCalculator
    {
		//------------------------ Constants --------------------------

		// Juristic Methods
		public static int Shafii = 0;    // Shafii (standard)
		public static int Hanafi = 1;    // Hanafi

		// Adjusting Methods for Higher Latitudes
		public static int None = 0;    // No adjustment
		public static int MidNight = 1;    // middle of night
		public static int OneSeventh = 2;    // 1/7th of night
		public static int AngleBased = 3;    // angle/60th of night

		// Time Names
		public static string[] TimeNames = { "Fajr", "Sunrise", "Dhuhr", "Asr", "Sunset", "Maghrib", "Isha" };
		static string InvalidTime = "----";  // The string used for inv

		//---------------------- Global Variables --------------------

		private readonly CalculationMethod calculationMethod;     // caculation method
		private readonly JuristicMethod asarJuristic;        // Juristic method for Asr
		private int dhuhrMinutes = 0;       // minutes after mid-day for Dhuhr
		private int adjustHighLats = 1; // adjusting method for higher latitudes

		private readonly TimeFormat timeFormat;     // time format

		private double lat;        // latitude
		private double lng;        // longitude
		private int timeZone;   // time-zone
		private double JDate;      // Julian date

		private int[] times;

		//--------------------- Technical Settings --------------------


		private int numIterations = 1;      // number of iterations needed to compute times



		//------------------- Calc Method Parameters --------------------

		private Dictionary<CalculationMethod, double[]> methodParameters = new Dictionary<CalculationMethod, double[]>
		{
			[CalculationMethod.Jafari] = new double[] { 16, 0, 4, 0, 14 },
			[CalculationMethod.Karachi] = new double[] { 18, 1, 0, 0, 18 },
			[CalculationMethod.ISNA] = new double[] { 15, 1, 0, 0, 15 },
			[CalculationMethod.MWL] = new double[] { 18, 1, 0, 0, 17 },
			[CalculationMethod.Makkah] = new double[] { 18.5, 1, 0, 1, 90 },
			[CalculationMethod.Egypt] = new double[] { 19.5, 1, 0, 0, 17.5 },
			[CalculationMethod.Tehran] = new double[] { 17.7, 0, 4.5, 0, 14 },
			[CalculationMethod.Custom] = new double[] { 18, 1, 0, 0, 17 },
		};

		public NamazTimeCalculator(
			CalculationMethod calculationMethod,
			JuristicMethod juristicMethod,
			TimeFormat timeFormat)
        {
			times = new int[7];
			this.calculationMethod = calculationMethod;
			this.asarJuristic = juristicMethod;
			this.timeFormat = timeFormat;
		}

		// return prayer times for a given date
		public string[] GetPrayerTimes(
			int year,
			int month,
			int day,
			double latitude,
			double longitude,
			int timeZone)
		{
			return this.GetDatePrayerTimes(year, month + 1, day, latitude, longitude, timeZone);
		}

		// set the angle for calculating Fajr
		public void SetFajrAngle(double angle)
		{
			this.SetCustomParams(new int[] { (int)angle, -1, -1, -1, -1 });
		}

		// set the angle for calculating Maghrib
		public void SetMaghribAngle(double angle)
		{
			this.SetCustomParams(new int[] { -1, 0, (int)angle, -1, -1 });
		}

		// set the angle for calculating Isha
		public void SetIshaAngle(double angle)
		{
			this.SetCustomParams(new int[] { -1, -1, -1, 0, (int)angle });
		}

		// set the minutes after mid-day for calculating Dhuhr
		public void SetDhuhrMinutes(int minutes)
		{
			this.dhuhrMinutes = minutes;
		}

		// set the minutes after Sunset for calculating Maghrib
		public void SetMaghribMinutes(int minutes)
		{
			this.SetCustomParams(new int[] { -1, 1, minutes, -1, -1 });
		}

		// set the minutes after Maghrib for calculating Isha
		public void SetIshaMinutes(int minutes)
		{
			this.SetCustomParams(new int[] { -1, -1, -1, 1, minutes });
		}

		// set custom values for calculation parameters
		public void SetCustomParams(int[] param)
		{
			for (int i = 0; i < 5; i++)
			{
				if (param[i] == -1)
					this.methodParameters[CalculationMethod.Custom][i] = this.methodParameters[this.calculationMethod][i];
				else
					this.methodParameters[CalculationMethod.Custom][i] = param[i];
			}
			// TODO: this.calculationMethod = CalculationMethod.Custom;
		}

		// set adjusting method for higher latitudes
		public void SetHighLatsMethod(int methodID)
		{
			this.adjustHighLats = methodID;
		}

		// convert float hours to 24h format
		public string FloatToTime24(double time)
		{
			if (time < 0)
				return InvalidTime;
			time = this.FixHour(time + 0.5 / 60);  // add 0.5 minutes to round
			double hours = Math.Floor(time);
			double minutes = Math.Floor((time - hours) * 60);
			return this.twoDigitsFormat((int)hours) + ":" + this.twoDigitsFormat((int)minutes);
		}

		// convert float hours to 12h format
		public string FloatToTime12(double time, bool noSuffix)
		{
			if (time < 0)
				return InvalidTime;
			time = this.FixHour(time + 0.5 / 60);  // add 0.5 minutes to round
			double hours = Math.Floor(time);
			double minutes = Math.Floor((time - hours) * 60);
			string suffix = hours >= 12 ? " pm" : " am";
			hours = (hours + 12 - 1) % 12 + 1;
			return ((int)hours) + ":" + this.twoDigitsFormat((int)minutes) + (noSuffix ? "" : suffix);
		}

		// convert float hours to 12h format with no suffix
		public string FloatToTime12NS(double time)
		{
			return this.FloatToTime12(time, true);
		}

		//---------------------- Compute Prayer Times -----------------------

		// return prayer times for a given date
		public string[] GetDatePrayerTimes(int year, int month, int day, double latitude, double longitude,

		int timeZone)
		{
			this.lat = latitude;
			this.lng = longitude;
			this.timeZone = timeZone;
			this.JDate = this.JulianDate(year, month, day) - longitude / (15 * 24);

			return this.ComputeDayTimes();
		}

		// compute declination angle of sun and equation of time
		public double[] SunPosition(double jd)
		{
			double D = jd - 2451545.0;
			double g = this.FixAngle(357.529 + 0.98560028 * D);
			double q = this.FixAngle(280.459 + 0.98564736 * D);
			double L = this.FixAngle(q + 1.915 * this.dsin(g) + 0.020 * this.dsin(2 * g));

			double R = 1.00014 - 0.01671 * this.dcos(g) - 0.00014 * this.dcos(2 * g);
			double e = 23.439 - 0.00000036 * D;

			double d = this.darcsin(this.dsin(e) * this.dsin(L));
			double RA = this.darctan2(this.dcos(e) * this.dsin(L), this.dcos(L)) / 15;
			RA = this.FixHour(RA);
			double EqT = q / 15 - RA;

			return new double[] { d, EqT };
		}

		// compute equation of time
		public double EquationOfTime(double jd)
		{
			return this.SunPosition(jd)[1];
		}

		// compute declination angle of sun
		public double SunDeclination(double jd)
		{
			return this.SunPosition(jd)[0];
		}

		// compute mid-day (Dhuhr, Zawal) time
		public double ComputeMidDay(double t)
		{
			double T = this.EquationOfTime(this.JDate + t);
			double Z = this.FixHour(12 - T);
			return Z;
		}

		// compute time for a given angle G
		public double ComputeTime(double G, double t)
		{
			//System.out.println("G: "+G);

			double D = this.SunDeclination(this.JDate + t);
			double Z = this.ComputeMidDay(t);
			double V = ((double)1 / 15) * this.darccos((-this.dsin(G) - this.dsin(D) * this.dsin(this.lat)) /
					(this.dcos(D) * this.dcos(this.lat)));
			return Z + (G > 90 ? -V : V);
		}

		// compute the time of Asr
		public double ComputeAsr(double t)  // Shafii: step=1, Hanafi: step=2
		{
			var step = (int)this.asarJuristic;
			double D = this.SunDeclination(this.JDate + t);
			double G = -this.darccot(step + this.dtan(Math.Abs(this.lat - D)));
			return this.ComputeTime(G, t);
		}

		//---------------------- Compute Prayer Times -----------------------

		// compute prayer times at given julian date
		public double[] ComputeTimes(double[] times)
		{
			double[] t = this.DayPortion(times);

			double fajr = this.ComputeTime(180 - this.methodParameters[this.calculationMethod][0], t[0]);
			double sunrise = this.ComputeTime(180 - 0.833, t[1]);
			double dhuhr = this.ComputeMidDay(t[2]);
			double asr = this.ComputeAsr(t[3]);
			double sunset = this.ComputeTime(0.833, t[4]); ;
			double maghrib = this.ComputeTime(this.methodParameters[this.calculationMethod][2], t[5]);
			double isha = this.ComputeTime(this.methodParameters[this.calculationMethod][4], t[6]);

			return new double[] { fajr, sunrise, dhuhr, asr, sunset, maghrib, isha };
		}

		// adjust Fajr, Isha and Maghrib for locations in higher latitudes
		public double[] AdjustHighLatTimes(double[] times)
		{
			double nightTime = this.GetTimeDifference(times[4], times[1]); // sunset to sunrise

			// Adjust Fajr
			double FajrDiff = this.NightPortion(this.methodParameters[this.calculationMethod][0]) * nightTime;
			if (this.GetTimeDifference(times[0], times[1]) > FajrDiff)
				times[0] = times[1] - FajrDiff;

			// Adjust Isha
			double IshaAngle = (this.methodParameters[this.calculationMethod][3] == 0)
				? this.methodParameters[this.calculationMethod][4]
				: 18;
			double IshaDiff = this.NightPortion(IshaAngle) * nightTime;
			if (this.GetTimeDifference(times[4], times[6]) > IshaDiff)
				times[6] = times[4] + IshaDiff;

			// Adjust Maghrib
			double MaghribAngle = (methodParameters[this.calculationMethod][1] == 0)
				? this.methodParameters[this.calculationMethod][2]
				: 4;
			double MaghribDiff = this.NightPortion(MaghribAngle) * nightTime;
			if (this.GetTimeDifference(times[4], times[5]) > MaghribDiff)
				times[5] = times[4] + MaghribDiff;

			return times;
		}

		// the night portion used for adjusting times in higher latitudes
		public double NightPortion(double angle)
		{
			double val = 0;
			if (this.adjustHighLats == AngleBased)
				val = 1.0 / 60.0 * angle;
			if (this.adjustHighLats == MidNight)
				val = 1.0 / 2.0;
			if (this.adjustHighLats == OneSeventh)
				val = 1.0 / 7.0;

			return val;
		}

		public double[] DayPortion(double[] times)
		{
			for (int i = 0; i < times.Length; i++)
			{
				times[i] /= 24;
			}
			return times;
		}

		// compute prayer times at given julian date
		public string[] ComputeDayTimes()
		{
			double[] times = { 5, 6, 12, 13, 18, 18, 18 }; //default times

			for (int i = 0; i < this.numIterations; i++)
			{
				times = this.ComputeTimes(times);
			}

			times = this.AdjustTimes(times);
			return this.AdjustTimesFormat(times);
		}


		// adjust times in a prayer time array
		public double[] AdjustTimes(double[] times)
		{
			for (int i = 0; i < 7; i++)
			{
				times[i] += this.timeZone - this.lng / 15;
			}
			times[2] += this.dhuhrMinutes / 60; //Dhuhr
			if (this.methodParameters[this.calculationMethod][1] == 1) // Maghrib
				times[5] = times[4] + this.methodParameters[this.calculationMethod][2] / 60.0;
			if (this.methodParameters[this.calculationMethod][3] == 1) // Isha
				times[6] = times[5] + this.methodParameters[this.calculationMethod][4] / 60.0;

			if (this.adjustHighLats != None)
			{
				times = this.AdjustHighLatTimes(times);
			}

			return times;
		}

		public string[] AdjustTimesFormat(double[] times)
		{
			string[] formatted = new String[times.Length];

			if (this.timeFormat == TimeFormat.Floating)
			{
				for (int i = 0; i < times.Length; ++i)
				{
					formatted[i] = times[i] + "";
				}
				return formatted;
			}
			for (int i = 0; i < 7; i++)
			{
				if (this.timeFormat == TimeFormat.Time12)
					formatted[i] = this.FloatToTime12(times[i], true);
				else if (this.timeFormat == TimeFormat.Time12NS)
					formatted[i] = this.FloatToTime12NS(times[i]);
				else
					formatted[i] = this.FloatToTime24(times[i]);
			}
			return formatted;
		}

		//---------------------- Misc Functions -----------------------

		// compute the difference between two times
		public double GetTimeDifference(double c1, double c2)
		{
			double diff = this.FixHour(c2 - c1); ;
			return diff;
		}

		// add a leading 0 if necessary
		public String twoDigitsFormat(int num)
		{

			return (num < 10) ? "0" + num : num + "";
		}

		//---------------------- Julian Date Functions -----------------------

		// calculate julian date from a calendar date
		public double JulianDate(int year, int month, int day)
		{
			if (month <= 2)
			{
				year -= 1;
				month += 12;
			}
			double A = (double)Math.Floor(year / 100.0);
			double B = 2 - A + Math.Floor(A / 4);

			double JD = Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + B - 1524.5;
			return JD;
		}


		//---------------------- Time-Zone Functions -----------------------


		// detect daylight saving in a given date
		public bool UseDayLightaving(int year, int month, int day)
		{
			return TimeZone.CurrentTimeZone.IsDaylightSavingTime(new DateTime(year, month, day));
		}

		// ---------------------- Trigonometric Functions -----------------------

		// degree sin
		public double dsin(double d)
		{
			return Math.Sin(this.DegreeToRadian(d));
		}

		// degree cos
		public double dcos(double d)
		{
			return Math.Cos(this.DegreeToRadian(d));
		}

		// degree tan
		public double dtan(double d)
		{
			return Math.Tan(this.DegreeToRadian(d));
		}

		// degree arcsin
		public double darcsin(double x)
		{
			return this.RadianToDegree(Math.Asin(x));
		}

		// degree arccos
		public double darccos(double x)
		{
			return this.RadianToDegree(Math.Acos(x));
		}

		// degree arctan
		public double darctan(double x)
		{
			return this.RadianToDegree(Math.Atan(x));
		}

		// degree arctan2
		public double darctan2(double y, double x)
		{
			return this.RadianToDegree(Math.Atan2(y, x));
		}

		// degree arccot
		public double darccot(double x)
		{
			return this.RadianToDegree(Math.Atan(1 / x));
		}


		// Radian to Degree
		public double RadianToDegree(double radian)
		{
			return (radian * 180.0) / Math.PI;
		}

		// degree to radian
		public double DegreeToRadian(double degree)
		{
			return (degree * Math.PI) / 180.0;
		}

		public double FixAngle(double angel)
		{
			angel = angel - 360.0 * (Math.Floor(angel / 360.0));
			angel = angel < 0 ? angel + 360.0 : angel;
			return angel;
		}

		// range reduce hours to 0..23
		public double FixHour(double hour)
		{
			hour = hour - 24.0 * (Math.Floor(hour / 24.0));
			hour = hour < 0 ? hour + 24.0 : hour;
			return hour;
		}

		public static void Sample()
        {
			var p = new NamazTimeCalculatorBuilder()
				.WithCalculationMethod(CalculationMethod.ISNA)
				.WithAsarMethod(JuristicMethod.Hanafi)
				.Build();
			double lo = 25;
			double la = 55;
			int y = 0, m = 0, d = 0, tz = 0;

			DateTime cc = DateTime.Now;
			y = cc.Year;
			m = cc.Month;
			d = cc.Day;
			tz = (DateTime.UtcNow - DateTime.Now).Hours;
			String[] s;

			s = p.GetDatePrayerTimes(y, m, d, lo, la, tz);
			for (int i = 0; i < s.Length; ++i)
			{
				Console.WriteLine(s[i]);
			}
		}
	}
}
