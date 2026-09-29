#region Copyright

// ---------------------------------------------------------------------------
// Copyright © 2023 to the present, Crestron Electronics®, Inc.
// All Rights Reserved.
// No part of this software may be reproduced in any form, machine
// or natural, without the express written consent of Crestron Electronics.
// Use of this source code is subject to the terms of the Crestron Software
// License Agreement under which you licensed this source code.
// ---------------------------------------------------------------------------

#endregion

using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace RainPoint.CrestronDriver
	{
	/// <summary>
	/// Describes a discovered timer for the platform's managed-device catalog.
	/// </summary>
	/// <param name="uxCategory">The Crestron discovery category.</param>
	/// <param name="name">The assigned display name.</param>
	/// <param name="manufacturer">The manufacturer display name.</param>
	/// <param name="model">The reported timer model identifier.</param>
	/// <param name="serialNumber">The stable identifier advertised for this child device.</param>
	[EntityDataType (Id = "platform:ManagedDevice")]
	public class PlatformManagedDevice (
		 DeviceUxCategory uxCategory,
		 string name,
		 string manufacturer,
		 string model,
		 string serialNumber
		 )
		{
		/// <summary>
		/// Gets the category used to place the timer in Crestron Home discovery.
		/// </summary>
		[EntityProperty]
		public DeviceUxCategory UxCategory { get; private set; } = uxCategory;

		/// <summary>
		/// Gets the timer's assigned display name.
		/// </summary>
		[EntityProperty]
		public string Name { get; private set; } = name;

		/// <summary>
		/// Gets the manufacturer displayed during discovery.
		/// </summary>
		[EntityProperty]
		public string Manufacturer { get; private set; } = manufacturer;

		/// <summary>
		/// Gets the reported timer model identifier.
		/// </summary>
		[EntityProperty]
		public string Model { get; private set; } = model;

		/// <summary>
		/// Gets the stable hub/address-based identifier used for this child device.
		/// </summary>
		[EntityProperty]
		public string SerialNumber { get; private set; } = serialNumber;
		}
	}