# User guide

JouleDesk shows how much power a Windows PC uses and what it costs. It needs no smart plug or other extra hardware: the figures come from the processor's energy counter, the graphics card's driver and adjustable estimates.

## Getting started

Run the installer from the Releases page. English is selected at first; choose another language before continuing if you prefer. The languages are English, Finnish, Swedish, German, French, Spanish, Portuguese, Italian, Russian, Simplified Chinese, Japanese and Korean.

The language is chosen during installation. To change it later, quit the app and run the installer again; there is no language switch inside the app. If the app is started without the installer, it uses the Windows display language when it is supported and English otherwise.

The installer creates a Start menu entry and offers a desktop shortcut and automatic start when you sign in to Windows. You can pin the installed app to the taskbar with Windows.

## Background monitoring

Closing the window hides it in the notification area and monitoring continues. Click the notification icon, or start JouleDesk again, to show the existing window. To stop the app completely, choose **Settings → Quit app and monitoring**.

Automatic monitoring starts when you sign in, not before. No consumption is made up for shutdown, sleep, paused monitoring or intervals without sensor readings. The **Pause** button on the overview stops sampling until you resume it.

## Window layout

Resize the window from its edges, or drag its title bar to a screen edge or corner to use Windows Snap. Cards and fields rearrange to fit the width. In small windows the navigation moves above the content.

Scroll with the mouse wheel, the scroll bar or Page Up and Page Down. Tabbing to a field brings it into view. What you have typed in a form survives resizing the window, but not leaving the page.

## Power readings

CPU power is calculated from the change in the Windows energy counter of the processor package (Intel RAPL). GPU power comes from NVIDIA's `nvidia-smi` tool. These figures cover those two components, not the whole PC at the wall.

Memory, drives, the motherboard, cooling and other parts are estimates, and the **Components** page labels each one as a sensor reading, a model-based estimate or a general estimate. The bundled profiles cover the Kingston KC3000 1 TB and FURY Renegade 1 TB drives. A drive's estimate follows its activity as reported by Windows; other drives use a general assumption. Memory defaults to 4 W per module and the other parts to 20 W. Change these in **Settings**, or switch to a single combined estimate for everything except the CPU and GPU.

The estimated draw at the wall is `(CPU + GPU + other parts) / PSU efficiency`. The default efficiency is 90 %. A power supply's rated wattage is its capacity, not what the computer draws. Displays and external devices are not included. Measuring the real wall-socket consumption takes an external electricity meter.

An optional HTTPS address in Settings can supply more drive profiles, in the format of `catalog-example.json`. No universal component database is configured by default. **Data sources** on the Components page lists where each figure comes from.

## Electricity prices

The **Electricity prices** page starts with a bar chart of one day's prices. Each bar is one price period, hourly or quarter-hourly as the market publishes them, and its colour runs from mint for the cheapest periods of the day through amber to coral for the dearest. A dashed horizontal line marks the day's average. On today a dashed vertical line marks the current moment, and the periods that have passed fade. Point at a bar to see its time and price in a bubble above it. The arrows above the chart move between the days that have prices; tomorrow's prices usually appear in the afternoon.

The chart shows the spot price, or the spot price plus your contract charges when you use the **Spot + charges** plan. A fixed-price plan still shows the spot price here for reference. Prices are cents per kWh including VAT, in Finnish time. Below the chart the list shows the current and coming periods with the price under your selected plan.

Prices come from [Pörssisähkö.net](https://www.porssisahko.net/), a free service run by one person, when the app starts and about twice a day after that. In the afternoon, until the next day's prices have been published, it asks every half hour. **Refresh prices** fetches them at once. The prices are cached on your PC. Negative prices are supported. A missing price stays unknown instead of being treated as free electricity, and it is filled in later if the service provides it.

## Electricity plans

Choose one of three plans under **Electricity plan**:

- **Spot electricity:** the spot price only.
- **Spot + charges:** the spot price plus your contract margin, distribution charge and electricity tax.
- **Fixed price:** your energy price plus distribution charge and electricity tax. The spot margin is not added.

Enter prices in cents per kWh including VAT. Monthly fixed fees are not divided among your devices.

Changing the plan prices all recorded consumption again with the new prices, so you can compare plans. It does not keep a billing record of earlier contracts. The recorded kWh never change.

## History, export and backups

The **History** page opens with a curve of the energy recorded on each day of a month; while the month is current, the stretch up to today is dashed because the day is not over. Above the chart are the month's total energy, its cost and the busiest day; the arrow buttons move to the previous and next month that have recordings, and pointing at the curve shows the nearest day's energy and cost. Below the chart the days of the month are listed with their cost under the selected plan. A day marked with an asterisk includes energy that has no spot price yet, so its cost is partial. **Export CSV** saves every recorded interval; readings are stored in records of at most a quarter hour. A CSV file has English column headings, decimal points and UTC times regardless of the interface language, and an empty cost where the price is unknown.

Settings, cached prices and history are stored in `%LOCALAPPDATA%\JouleDesk`; **Open data folder** in Settings shows it. To back up your records, quit the app and copy the folder. Uninstalling removes the program and its startup entry but keeps this folder; delete it to remove your records.

## Privacy

JouleDesk has no account, analytics or crash reporting, and it sends nothing about you or your PC anywhere. Your consumption, settings and the hardware it reads (drive models, memory part numbers, the CPU energy counter and the GPU power) stay on your PC.

It makes two kinds of request over HTTPS. The first fetches spot prices from Pörssisähkö.net, as described above; like any web request it shows your IP address, and the name and version of the app, to that service. The second fetches the drive profile catalog from the address you enter in Settings, if you enter one, when the app starts and when you press **Get update**; that address and its server are yours to choose.

## Updating and troubleshooting

Quit the app from Settings before running a new installer. If the installer says JouleDesk is running, close the background process; closing the window is not enough.

If a reading is missing, look at the Components page. GPU power needs a supported NVIDIA driver, and the CPU energy counter is not available on every machine. Without both the CPU and the GPU reading no consumption can be recorded.

The installer includes the .NET runtime, so nothing else needs to be installed. The installer is not code signed, so Windows may ask for confirmation.
